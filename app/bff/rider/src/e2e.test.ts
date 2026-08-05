import { strict as assert } from 'node:assert';
import { spawn, spawnSync, ChildProcess } from 'node:child_process';
import { after, before, test } from 'node:test';
import { covers, untraced } from '@azimuth/annotations';
import { server } from './server';

/**
 * End to end, in D15's sense: real process boundaries and real transport between the components
 * under test. The test speaks HTTP to the BFF, the BFF speaks HTTP to the trip service, and the
 * trip service speaks to a real Postgres.
 *
 * These claims are covered here rather than in the service's own suite because each site can pass
 * in isolation while the composition leaks — which is exactly why the verification plan raises them
 * to `e2e`, and why the trip service's unit tests were reported as `wrong-form` until this existed.
 */

const PG = 'azimuth-e2e-pg';
const TRIP_PORT = 5081;
const BFF_PORT = 5091;
const BFF = `http://127.0.0.1:${BFF_PORT}`;

let trip: ChildProcess | undefined;

function docker(...args: string[]) {
  return spawnSync('docker', args, { encoding: 'utf8' });
}

async function waitFor(probe: () => Promise<boolean>, what: string, attempts = 120) {
  for (let i = 0; i < attempts; i++) {
    if (await probe().catch(() => false)) return;
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  throw new Error(`timed out waiting for ${what}`);
}

async function post(path: string, body: unknown) {
  const response = await fetch(`${BFF}${path}`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
  });
  return { status: response.status, body: (await response.json()) as any };
}

async function get(path: string) {
  const response = await fetch(`${BFF}${path}`);
  return { status: response.status, body: (await response.json()) as any };
}

/** Reaches past the BFF to move the trip, standing in for the driver app slice 3 adds. */
async function driver(path: string) {
  const response = await fetch(`http://127.0.0.1:${TRIP_PORT}${path}`, { method: 'POST' });
  return response.status;
}

before(async () => {
  docker('rm', '-f', PG);
  const started = docker(
    'run', '-d', '--name', PG, '-e', 'POSTGRES_PASSWORD=postgres',
    '-e', 'POSTGRES_DB=trip', '-P', 'postgres:17-alpine',
  );
  assert.equal(started.status, 0, started.stderr);

  const portLine = docker('port', PG, '5432/tcp').stdout.trim().split('\n')[0];
  const pgPort = portLine.split(':').pop();
  const connection = `Host=127.0.0.1;Port=${pgPort};Database=trip;Username=postgres;Password=postgres`;

  await waitFor(async () => docker('exec', PG, 'pg_isready', '-U', 'postgres').status === 0, 'postgres');

  trip = spawn(
    'dotnet',
    ['run', '--no-build', '--project', '../../services/Trip/Trip.csproj'],
    {
      env: {
        ...process.env,
        TRIP_DB: connection,
        ASPNETCORE_URLS: `http://127.0.0.1:${TRIP_PORT}`,
        ASPNETCORE_ENVIRONMENT: 'Development',
      },
      stdio: 'ignore',
    },
  );

  await waitFor(
    async () => (await fetch(`http://127.0.0.1:${TRIP_PORT}/quotes/${'0'.repeat(8)}-0000-0000-0000-000000000000`)).status < 500,
    'trip service',
  );

  // Seeded after the service has migrated. A driver row is fixture data, not a claim — the driver
  // side arrives in slice 3 (D16.3).
  const seeded = docker(
    'exec', PG, 'psql', '-U', 'postgres', '-d', 'trip', '-c',
    "INSERT INTO drivers (id, available, near, display, vehicle, position) " +
      "VALUES ('driver-e2e', true, 'downtown', 'Sam', 'blue hatchback', '52.37,4.89') " +
      'ON CONFLICT (id) DO NOTHING',
  );
  assert.equal(seeded.status, 0, seeded.stderr);

  process.env.TRIP_URL = `http://127.0.0.1:${TRIP_PORT}`;
  await new Promise<void>((resolve) => server.listen(BFF_PORT, resolve));
}, { timeout: 180_000 });

after(async () => {
  trip?.kill();
  await new Promise<void>((resolve) => server.close(() => resolve()));
  docker('rm', '-f', PG);
});

async function requestedTrip(rider: string) {
  const quote = await post('/rider/quotes', {
    pickup: 'a', dropoff: 'b', baseMinor: 500, distanceMinor: 1000, currency: 'EUR',
  });
  assert.equal(quote.status, 200);
  const created = await post('/rider/trips', { riderId: rider, quoteId: quote.body.id });
  assert.equal(created.status, 200, JSON.stringify(created.body));
  return created.body.id as string;
}

test('a rider sees no individual driver before assignment', async () => {
  covers('trip/rider-view', 'no-driver-position-before-assignment', 'e2e', 'invariant');
  covers('trip/rider-view', 'no-driver-identity-before-assignment', 'e2e', 'invariant');
  covers('trip/rider-view', 'supply-density-shown-before-assignment', 'e2e', 'invariant');

  const id = await requestedTrip(`rider-${Date.now()}-a`);
  const view = await get(`/rider/trips/${id}`);

  assert.equal(view.body.driver, null);
  assert.equal(view.body.driverPosition, null);
  assert.equal(typeof view.body.supplyDensity, 'string');
  assert.equal(JSON.stringify(view.body).includes('52.37'), false);
});

test("the driver's position reaches the rider only while the trip is live", async () => {
  covers('trip/rider-view', 'driver-position-follows-driver', 'e2e', 'invariant');
  covers('trip/rider-view', 'driver-shown-after-assignment', 'e2e', 'invariant');
  covers('trip/rider-view', 'no-position-after-completion', 'e2e', 'invariant');
  covers('trip/rider-view', 'driver-identity-remains-on-receipt', 'e2e', 'invariant');

  const id = await requestedTrip(`rider-${Date.now()}-b`);
  assert.equal(await driver(`/trips/${id}/accept/driver-e2e`), 200);

  const assigned = await get(`/rider/trips/${id}`);
  assert.equal(assigned.body.driver?.name, 'Sam');
  assert.equal(assigned.body.driverPosition, '52.37,4.89');

  assert.equal(await driver(`/trips/${id}/start?actor=driver-e2e`), 200);
  assert.equal((await get(`/rider/trips/${id}`)).body.driverPosition, '52.37,4.89');

  assert.equal(await driver(`/trips/${id}/complete?actor=driver-e2e`), 200);
  const done = await get(`/rider/trips/${id}`);
  assert.equal(done.body.driverPosition, null);
  assert.equal(done.body.driver?.name, 'Sam');
  assert.equal(JSON.stringify(done.body).includes('52.37'), false);
});

test('a cancelled trip shows no position either', async () => {
  covers('trip/rider-view', 'no-position-after-cancellation', 'e2e', 'invariant');

  const id = await requestedTrip(`rider-${Date.now()}-c`);
  assert.equal(await driver(`/trips/${id}/cancel?actor=rider`), 200);

  const view = await get(`/rider/trips/${id}`);
  assert.equal(view.body.state, 'cancelled');
  assert.equal(view.body.driverPosition, null);
});

test('a rider is told their trip exists and is awaiting a driver', async () => {
  covers('trip/request', 'rider-informed-of-trip', 'e2e', 'invariant');
  covers('trip/request', 'request-admitted-with-valid-quote', 'e2e', 'invariant');

  const id = await requestedTrip(`rider-${Date.now()}-d`);
  const view = await get(`/rider/trips/${id}`);
  assert.equal(view.body.awaitingDriver, true);
  assert.equal(view.body.state, 'requested');
});

test('a fare outside every serviced area is refused', async () => {
  covers('pricing/quote', 'unserviceable-area', 'e2e', 'invariant');

  const quote = await post('/rider/quotes', {
    pickup: '', dropoff: 'b', baseMinor: 500, distanceMinor: 1000, currency: 'EUR',
  });
  assert.equal(quote.status, 400);
  assert.equal(quote.body.error, 'unserviceable-area');
});

/**
 * Was a defect test asserting the leak; now a regression test asserting its absence.
 *
 * The receipt is a second rider-reachable surface. It satisfied every behavioural claim in
 * `trip/rider-view` and leaked a completed trip's driver position anyway, and the matrix reported
 * no new hole — which is what the invariant over the site class was written against.
 */
test('no rider-reachable surface carries a position outside the live phases', async () => {
  covers('trip/rider-view', 'position-confined-to-live-phases', 'e2e', 'invariant');

  const id = await requestedTrip(`rider-${Date.now()}-leak`);
  await driver(`/trips/${id}/accept/driver-e2e`);
  await driver(`/trips/${id}/start?actor=driver-e2e`);
  await driver(`/trips/${id}/complete?actor=driver-e2e`);

  // The path the claims cover is clean.
  const view = await get(`/rider/trips/${id}`);
  assert.equal(view.body.driverPosition, null);
  assert.equal(JSON.stringify(view.body).includes('52.37'), false);

  // And so is the receipt, which is the surface that used to leak.
  const receipt = await get(`/rider/trips/${id}/receipt`);
  assert.equal(JSON.stringify(receipt.body).includes('52.37'), false);
  assert.equal(receipt.body.driver?.name, 'Sam');

  // The raw route the receipt used to reach through carries no position either.
  const raw = await fetch(`http://127.0.0.1:${TRIP_PORT}/trips/${id}/driver`);
  assert.equal(JSON.stringify(await raw.json()).includes('52.37'), false);
});

test('the stack is reachable', () => {
  untraced('a smoke check for the harness itself; maps to no claim');
  assert.ok(trip);
});
