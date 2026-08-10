import { strict as assert } from 'node:assert';
import { spawn, spawnSync, ChildProcess } from 'node:child_process';
import { createConnection } from 'node:net';
import * as path from 'node:path';
import { after, before, test } from 'node:test';
import { covers } from '@azimuth/annotations';

/**
 * End to end, in D15's sense: real process boundaries and real transport between the components
 * under test. The test speaks HTTP to the rider app, the rider app's route handlers speak HTTP to
 * the trip service, and the trip service speaks to a real Postgres.
 *
 * These claims are covered here rather than in the service's own suite because each site can pass
 * in isolation while the composition leaks — which is exactly why the verification plan raises them
 * to `e2e`, and why the trip service's unit tests were reported as `wrong-form` until this existed.
 *
 * The two web apps are separate Next.js processes for the same reason the two BFFs were separate
 * modules: the rider and driver visibility rules are opposite, and D1 calls that asymmetry
 * non-negotiable.
 */

const PG = 'azimuth-e2e-pg';
const RABBIT = 'azimuth-e2e-rabbit';
const PRICING_PORT = 5071;
const TRIP_PORT = 5081;
const PAYMENTS_PORT = 5086;
const ANALYTICS_PORT = 5088;
const RIDER_PORT = 5091;
const DRIVER_PORT = 5096;
const RIDER = `http://127.0.0.1:${RIDER_PORT}`;
const DRIVER_WEB = `http://127.0.0.1:${DRIVER_PORT}`;

const APP_ROOT = path.join(__dirname, '..', '..');

let trip: ChildProcess | undefined;
let pricing: ChildProcess | undefined;
let payments: ChildProcess | undefined;
let analytics: ChildProcess | undefined;
let riderWeb: ChildProcess | undefined;
let driverWeb: ChildProcess | undefined;
let createdTripCount = 0;
let completedTripCount = 0;

function docker(...args: string[]) {
  return spawnSync('docker', args, { encoding: 'utf8' });
}

async function waitFor(probe: () => Promise<boolean>, what: string, attempts = 240) {
  for (let i = 0; i < attempts; i++) {
    if (await probe().catch(() => false)) return;
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  throw new Error(`timed out waiting for ${what}`);
}

function portIsOpen(port: number): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = createConnection({ host: '127.0.0.1', port });
    socket.setTimeout(500);
    socket.once('connect', () => {
      socket.destroy();
      resolve(true);
    });
    socket.once('timeout', () => {
      socket.destroy();
      resolve(false);
    });
    socket.once('error', () => resolve(false));
  });
}

/** Starts a built Next app on a fixed port, pointed at the trip service. */
function startWeb(
  app: string,
  port: number,
  tripUrl: string,
  pricingUrl: string,
  paymentsUrl: string,
): ChildProcess {
  return spawn('npx', ['next', 'start', '-p', String(port)], {
    cwd: path.join(APP_ROOT, 'web', app),
    env: {
      ...process.env,
      TRIP_URL: tripUrl,
      PRICING_URL: pricingUrl,
      PAYMENTS_URL: paymentsUrl,
      NODE_ENV: 'production',
    },
    stdio: 'ignore',
  });
}

async function post(path: string, body: unknown) {
  const response = await fetch(`${RIDER}${path}`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
  });
  return { status: response.status, body: (await response.json()) as any };
}

async function get(path: string) {
  const response = await fetch(`${RIDER}${path}`);
  return { status: response.status, body: (await response.json()) as any };
}

/** The rendered page, not its API. A page is a rider-reachable site like any other. */
async function page(base: string, path: string) {
  const response = await fetch(`${base}${path}`);
  return { status: response.status, html: await response.text() };
}

async function servicePost(base: string, path: string, body?: unknown) {
  const response = await fetch(`${base}${path}`, {
    method: 'POST',
    headers: body === undefined ? undefined : { 'content-type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  return { status: response.status, body: text.length > 0 ? JSON.parse(text) as any : null };
}

/** Reaches past the apps to move the trip, standing in for the dispatch the driver app triggers. */
async function driver(path: string) {
  const response = await fetch(`http://127.0.0.1:${TRIP_PORT}${path}`, { method: 'POST' });
  if (response.status === 200 && path.includes('/complete?')) {
    completedTripCount++;
  }
  return response.status;
}

before(async () => {
  docker('rm', '-f', PG);
  docker('rm', '-f', RABBIT);
  const started = docker(
    'run', '-d', '--name', PG, '-e', 'POSTGRES_PASSWORD=postgres',
    '-e', 'POSTGRES_DB=trip', '-P', 'postgres:17-alpine',
  );
  assert.equal(started.status, 0, started.stderr);

  const portLine = docker('port', PG, '5432/tcp').stdout.trim().split('\n')[0];
  const pgPort = portLine.split(':').pop();
  const connection = `Host=127.0.0.1;Port=${pgPort};Database=trip;Username=postgres;Password=postgres`;

  await waitFor(async () => docker('exec', PG, 'pg_isready', '-U', 'postgres').status === 0, 'postgres');

  const rabbitStarted = docker('run', '-d', '--name', RABBIT, '-P', 'rabbitmq:4.1-alpine');
  assert.equal(rabbitStarted.status, 0, rabbitStarted.stderr);
  const rabbitPortLine = docker('port', RABBIT, '5672/tcp').stdout.trim().split('\n')[0];
  const rabbitPort = rabbitPortLine.split(':').pop();
  const rabbitUri = `amqp://guest:guest@127.0.0.1:${rabbitPort}/`;
  await waitFor(() => portIsOpen(Number(rabbitPort)), 'rabbitmq');

  const pricingUrl = `http://127.0.0.1:${PRICING_PORT}`;
  pricing = spawn(
    'dotnet',
    ['run', '--no-build', '--project', path.join(APP_ROOT, 'services/Pricing.Service/Pricing.Service.csproj')],
    {
      env: {
        ...process.env,
        PRICING_DB: connection,
        ASPNETCORE_URLS: pricingUrl,
        ASPNETCORE_ENVIRONMENT: 'Development',
      },
      stdio: 'ignore',
    },
  );
  await waitFor(
    async () => (await fetch(`${pricingUrl}/quotes/${'0'.repeat(13)}`)).status < 500,
    'pricing service',
  );

  const paymentsUrl = `http://127.0.0.1:${PAYMENTS_PORT}`;
  payments = spawn(
    'dotnet',
    ['run', '--no-build', '--project', path.join(APP_ROOT, 'services/Payments/Payments.csproj')],
    {
      env: {
        ...process.env,
        PAYMENTS_DB: connection,
        RABBITMQ_URI: rabbitUri,
        ASPNETCORE_URLS: paymentsUrl,
        ASPNETCORE_ENVIRONMENT: 'Development',
      },
      stdio: 'ignore',
    },
  );
  await waitFor(
    async () => (await fetch(`${paymentsUrl}/captures/${'0'.repeat(13)}`)).status < 500,
    'payments service',
  );

  const analyticsUrl = `http://127.0.0.1:${ANALYTICS_PORT}`;
  analytics = spawn(
    'dotnet',
    ['run', '--no-build', '--project', path.join(APP_ROOT, 'services/Analytics/Analytics.csproj')],
    {
      env: {
        ...process.env,
        ANALYTICS_DB: connection,
        RABBITMQ_URI: rabbitUri,
        ASPNETCORE_URLS: analyticsUrl,
        ASPNETCORE_ENVIRONMENT: 'Development',
      },
      stdio: 'ignore',
    },
  );
  await waitFor(
    async () => (await fetch(`${analyticsUrl}/activity/summary`)).status < 500,
    'analytics service',
  );

  trip = spawn(
    'dotnet',
    ['run', '--no-build', '--project', path.join(APP_ROOT, 'services/Trips/Trips.csproj')],
    {
      env: {
        ...process.env,
        TRIP_DB: connection,
        RABBITMQ_URI: rabbitUri,
        ASPNETCORE_URLS: `http://127.0.0.1:${TRIP_PORT}`,
        ASPNETCORE_ENVIRONMENT: 'Development',
      },
      stdio: 'ignore',
    },
  );

  const tripUrl = `http://127.0.0.1:${TRIP_PORT}`;
  await waitFor(
    async () => (await fetch(`${tripUrl}/quotes/${'0'.repeat(13)}`)).status < 500,
    'trip service',
  );

  // Seeded after the service has migrated. A driver row is fixture data, not a claim.
  const seeded = docker(
    'exec', PG, 'psql', '-U', 'postgres', '-d', 'trip', '-c',
    "INSERT INTO drivers (id, available, near, display, vehicle, position) " +
      "VALUES ('driver-e2e', true, 'downtown', 'Sam', 'blue hatchback', '52.37,4.89') " +
      'ON CONFLICT (id) DO NOTHING',
  );
  assert.equal(seeded.status, 0, seeded.stderr);

  riderWeb = startWeb('rider', RIDER_PORT, tripUrl, pricingUrl, paymentsUrl);
  driverWeb = startWeb('driver', DRIVER_PORT, tripUrl, pricingUrl, paymentsUrl);

  await waitFor(async () => (await fetch(`${RIDER}/`)).status < 500, 'rider web');
  await waitFor(async () => (await fetch(`${DRIVER_WEB}/`)).status < 500, 'driver web');
}, { timeout: 300_000 });

after(async () => {
  trip?.kill();
  pricing?.kill();
  payments?.kill();
  analytics?.kill();
  riderWeb?.kill();
  driverWeb?.kill();
  docker('rm', '-f', PG);
  docker('rm', '-f', RABBIT);
});

/** Moves the seeded driver. Position is fixture data, so it changes the way it was seeded. */
function moveDriver(position: string) {
  const moved = docker(
    'exec', PG, 'psql', '-U', 'postgres', '-d', 'trip', '-c',
    `UPDATE drivers SET position = '${position}' WHERE id = 'driver-e2e'`,
  );
  assert.equal(moved.status, 0, moved.stderr);
}

async function requestedTrip(rider: string) {
  const quote = await post('/api/rider/quotes', {
    pickup: 'downtown', dropoff: 'airport', distanceMeters: 10000, currency: 'EUR',
  });
  assert.equal(quote.status, 200);
  const created = await post('/api/rider/trips', { riderId: rider, quoteToken: quote.body.token });
  assert.equal(created.status, 200, JSON.stringify(created.body));
  createdTripCount++;
  return created.body.id as string;
}

async function referralSummary(rider: string) {
  const summary = await get(`/api/rider/referrals/${encodeURIComponent(rider)}`);
  assert.equal(summary.status, 200, JSON.stringify(summary.body));
  return summary.body as {
    referralCode: string;
    attributionStatus: 'none' | 'pending' | 'qualified';
    credits: Array<{
      id: string;
      amountMinor: number;
      currency: string;
      status: 'available' | 'reserved' | 'used';
    }>;
  };
}

async function requestedReferralTrip(
  rider: string,
  options: { referralCode?: string; referralCreditId?: string } = {},
) {
  const quote = await post('/api/rider/quotes', {
    pickup: 'downtown', dropoff: 'airport', distanceMeters: 10000, currency: 'EUR',
  });
  assert.equal(quote.status, 200, JSON.stringify(quote.body));
  const created = await post('/api/rider/trips', {
    riderId: rider,
    quoteToken: quote.body.token,
    ...options,
  });
  assert.equal(created.status, 200, JSON.stringify(created.body));
  createdTripCount++;
  return {
    id: created.body.id as string,
    fareMinor: quote.body.fare.minor as number,
    currency: quote.body.fare.currency as string,
  };
}

async function completeTrip(id: string) {
  assert.equal(await driver(`/trips/${id}/accept/driver-e2e`), 200);
  assert.equal(await driver(`/trips/${id}/start?actor=driver-e2e`), 200);
  assert.equal(await driver(`/trips/${id}/complete?actor=driver-e2e`), 200);
}

test('a rider sees no individual driver before assignment', async () => {
  covers('trips/rider-view', 'no-driver-position-before-assignment', 'e2e', 'universal');
  covers('trips/rider-view', 'no-driver-identity-before-assignment', 'e2e', 'universal');
  covers('trips/rider-view', 'supply-density-shown-before-assignment', 'e2e', 'universal');

  const id = await requestedTrip(`rider-${Date.now()}-a`);
  const view = await get(`/api/rider/trips/${id}`);

  assert.equal(view.body.driver, null);
  assert.equal(view.body.driverPosition, null);
  assert.equal(typeof view.body.supplyDensity, 'string');
  assert.equal(JSON.stringify(view.body).includes('52.37'), false);
});

test("the driver's position reaches the rider only while the trip is live", async () => {
  covers('trips/rider-view', 'driver-position-follows-driver', 'e2e', 'universal');
  covers('trips/rider-view', 'driver-shown-after-assignment', 'e2e', 'universal');
  covers('trips/rider-view', 'no-position-after-completion', 'e2e', 'universal');
  covers('trips/rider-view', 'driver-identity-remains-on-receipt', 'e2e', 'universal');

  const id = await requestedTrip(`rider-${Date.now()}-b`);
  assert.equal(await driver(`/trips/${id}/accept/driver-e2e`), 200);

  const assigned = await get(`/api/rider/trips/${id}`);
  assert.equal(assigned.body.driver?.name, 'Sam');
  assert.equal(assigned.body.vehicle ?? assigned.body.driver?.vehicle, 'blue hatchback');
  assert.equal(assigned.body.driverPosition, '52.37,4.89');

  // The claim is that the shown position *follows* the driver, so the driver has to move. Asserting
  // the same literal twice passes against a projection that caches or hard-codes it.
  moveDriver('52.38,4.90');
  assert.equal((await get(`/api/rider/trips/${id}`)).body.driverPosition, '52.38,4.90');

  assert.equal(await driver(`/trips/${id}/start?actor=driver-e2e`), 200);
  moveDriver('52.39,4.91');
  assert.equal((await get(`/api/rider/trips/${id}`)).body.driverPosition, '52.39,4.91');

  assert.equal(await driver(`/trips/${id}/complete?actor=driver-e2e`), 200);
  const done = await get(`/api/rider/trips/${id}`);
  assert.equal(done.body.driverPosition, null);
  assert.equal(done.body.driver?.name, 'Sam');
  assert.equal(JSON.stringify(done.body).includes('52.3'), false);

  moveDriver('52.37,4.89');
});

test('a cancelled trip shows no position either', async () => {
  covers('trips/rider-view', 'no-position-after-cancellation', 'e2e', 'universal');
  covers('payments/capture', 'no-capture-on-cancellation-without-fee', 'e2e', 'example', 'contract');

  const id = await requestedTrip(`rider-${Date.now()}-c`);
  assert.equal(await driver(`/trips/${id}/cancel?actor=rider`), 200);

  const view = await get(`/api/rider/trips/${id}`);
  assert.equal(view.body.state, 'cancelled');
  assert.equal(view.body.driverPosition, null);

  const paymentsUrl = `http://127.0.0.1:${PAYMENTS_PORT}`;
  await servicePost(paymentsUrl, '/dispatch');
  assert.equal((await fetch(`${paymentsUrl}/captures/${id}`)).status, 404);
});

test('a rider is told their trip exists and is awaiting a driver', async () => {
  covers('trips/request', 'rider-informed-of-trip', 'e2e', 'example');
  covers('trips/request', 'request-admitted-with-valid-quote', 'e2e', 'example');

  const id = await requestedTrip(`rider-${Date.now()}-d`);
  const view = await get(`/api/rider/trips/${id}`);
  assert.equal(view.body.awaitingDriver, true);
  assert.equal(view.body.state, 'requested');
});

test('a fare outside every serviced area is refused', async () => {
  covers('pricing/quote', 'unserviceable-area', 'e2e', 'example');

  const quote = await post('/api/rider/quotes', {
    pickup: 'outside', dropoff: 'airport', distanceMeters: 10000, currency: 'EUR',
  });
  assert.equal(quote.status, 400);
  assert.equal(quote.body.errorCode, 'pricing:quote:issue:unserviceable_area');
});

/**
 * Was a defect test asserting the leak; now a regression test asserting its absence.
 *
 * The receipt is a second rider-reachable surface. It satisfied every behavioural claim in
 * `trips/rider-view` and leaked a completed trip's driver position anyway, and the matrix reported
 * no new hole — which is what the invariant over the site class was written against.
 *
 * The rendered pages are checked alongside the API, because a page is a site in that class too: the
 * class grew when the web app did, which is the whole point of quantifying over it.
 */
test('no rider-reachable surface carries a position outside the live phases', async () => {
  // Five named surfaces, not the class: membership is derived from the route table, and the
  // universal evidence for this claim is `invariant-breach` over that plus the type-level mechanism.
  covers('trips/rider-view', 'position-confined-to-live-phases', 'e2e', 'example');

  const id = await requestedTrip(`rider-${Date.now()}-leak`);
  await driver(`/trips/${id}/accept/driver-e2e`);
  await driver(`/trips/${id}/start?actor=driver-e2e`);
  await driver(`/trips/${id}/complete?actor=driver-e2e`);

  // The path the claims cover is clean.
  const view = await get(`/api/rider/trips/${id}`);
  assert.equal(view.body.driverPosition, null);
  assert.equal(JSON.stringify(view.body).includes('52.37'), false);

  // And so is the receipt, which is the surface that used to leak.
  const receipt = await get(`/api/rider/trips/${id}/receipt`);
  assert.equal(JSON.stringify(receipt.body).includes('52.37'), false);
  assert.equal(receipt.body.driver?.name, 'Sam');

  // The raw route the receipt used to reach through carries no position either.
  const raw = await fetch(`http://127.0.0.1:${TRIP_PORT}/trips/${id}/driver`);
  assert.equal(JSON.stringify(await raw.json()).includes('52.37'), false);

  // The rendered pages are sites in the same class.
  const tripPage = await page(RIDER, `/trips/${id}`);
  assert.equal(tripPage.status, 200);
  assert.equal(tripPage.html.includes('52.37'), false);

  const receiptPage = await page(RIDER, `/trips/${id}/receipt`);
  assert.equal(receiptPage.status, 200);
  assert.equal(receiptPage.html.includes('52.37'), false);
  assert.equal(receiptPage.html.includes('Sam'), true);
});

async function asDriver(path: string) {
  const response = await fetch(`${DRIVER_WEB}${path}`);
  return { status: response.status, body: (await response.json()) as any };
}

test('an offer shows the pickup and no rider', async () => {
  covers('trips/driver-view', 'pickup-shown-on-offer', 'e2e', 'example');
  covers('trips/driver-view', 'rider-contact-hidden-on-offer', 'e2e', 'example');

  const rider = `rider-${Date.now()}-drv`;
  const id = await requestedTrip(rider);
  const offer = await asDriver(`/api/driver/driver-e2e/offers/${id}`);

  assert.equal(offer.body.pickup, 'downtown');
  assert.equal(typeof offer.body.fare.minor, 'number');
  assert.equal(JSON.stringify(offer.body).includes(rider), false);
  assert.equal(JSON.stringify(offer.body).includes('proxy'), false);

  // The driver's own page carries no rider either.
  const offerPage = await page(DRIVER_WEB, `/drivers/driver-e2e/offers/${id}`);
  assert.equal(offerPage.status, 200);
  assert.equal(offerPage.html.includes(rider), false);
});

test('a rider contact reaches the driver only while they hold the trip', async () => {
  covers('trips/driver-view', 'proxy-contact-while-held', 'e2e', 'example');
  covers('trips/driver-view', 'contact-withdrawn-after-terminal', 'e2e', 'example');
  covers('trips/driver-view', 'rider-contact-confined-to-held-trips', 'e2e', 'example');

  const rider = `rider-${Date.now()}-hold`;
  const id = await requestedTrip(rider);

  // Before accepting, the driver holds nothing.
  const before = await asDriver(`/api/driver/driver-e2e/trips/${id}`);
  assert.equal(before.body.riderContact, null);

  await driver(`/trips/${id}/accept/driver-e2e`);
  const held = await asDriver(`/api/driver/driver-e2e/trips/${id}`);
  assert.equal(held.body.riderContact, `proxy:${rider}`);

  // Another driver holds nothing, even while the trip is live.
  const other = await asDriver(`/api/driver/driver-other/trips/${id}`);
  assert.equal(other.body.riderContact, null);

  await driver(`/trips/${id}/start?actor=driver-e2e`);
  await driver(`/trips/${id}/complete?actor=driver-e2e`);
  const done = await asDriver(`/api/driver/driver-e2e/trips/${id}`);
  assert.equal(done.body.riderContact, null);
  assert.equal(JSON.stringify(done.body).includes(rider), false);
});

test('surge survives quote admission and capture unchanged', async () => {
  covers('pricing/quote', 'current-pressure-selects-surge', 'e2e', 'example', 'contract');
  covers('pricing/quote', 'surge-is-a-quote-component', 'e2e', 'example', 'contract');
  covers('payments/capture', 'capture-created-on-completion', 'e2e', 'example', 'contract');
  covers('payments/capture', 'no-capture-before-completion', 'e2e', 'example', 'contract');
  covers('payments/capture', 'capture-equals-trip-fare', 'e2e', 'example', 'contract');
  covers('payments/capture', 'receipt-explains-payment-state', 'e2e', 'example', 'contract');
  covers('analytics/trip-activity', 'latest-version-is-projected', 'e2e', 'example', 'direct');

  const pricingUrl = `http://127.0.0.1:${PRICING_PORT}`;
  const paymentsUrl = `http://127.0.0.1:${PAYMENTS_PORT}`;
  const analyticsUrl = `http://127.0.0.1:${ANALYTICS_PORT}`;
  await waitFor(async () => {
    const summary = await (await fetch(`${analyticsUrl}/activity/summary`)).json() as any;
    const completed = summary.states.find((entry: any) => entry.state === 'completed')?.trips ?? 0;
    return summary.totalTrips === createdTripCount && completed === completedTripCount;
  }, 'analytics baseline');
  const summaryBefore = await (await fetch(`${analyticsUrl}/activity/summary`)).json() as any;
  const completedBefore = summaryBefore.states
    .find((entry: any) => entry.state === 'completed')?.trips ?? 0;
  const pressure = await servicePost(pricingUrl, '/market-pressure', {
    market: 'downtown', openRequests: 8, availableDrivers: 2,
  });
  assert.equal(pressure.status, 200, JSON.stringify(pressure.body));

  const quote = await post('/api/rider/quotes', {
    pickup: 'downtown', dropoff: 'airport', distanceMeters: 10000, currency: 'EUR',
  });
  assert.equal(quote.status, 200, JSON.stringify(quote.body));
  const surge = quote.body.breakdown.find((component: any) => component.label === 'surge');
  assert.ok(surge.amountMinor > 0);
  assert.equal(
    quote.body.breakdown.reduce((sum: number, component: any) => sum + component.amountMinor, 0),
    quote.body.fare.minor,
  );

  const created = await post('/api/rider/trips', {
    riderId: `rider-${Date.now()}-surge`, quoteToken: quote.body.token,
  });
  assert.equal(created.status, 200, JSON.stringify(created.body));
  createdTripCount++;
  const id = created.body.id as string;
  await servicePost(paymentsUrl, '/dispatch');
  assert.equal((await fetch(`${paymentsUrl}/captures/${id}`)).status, 404);
  await driver(`/trips/${id}/accept/driver-e2e`);
  await driver(`/trips/${id}/start?actor=driver-e2e`);
  await driver(`/trips/${id}/complete?actor=driver-e2e`);

  await waitFor(
    async () => (await fetch(`${paymentsUrl}/captures/${id}`)).status === 200,
    'automatic capture settlement',
  );
  const captured = await fetch(`${paymentsUrl}/captures/${id}`);
  assert.equal(captured.status, 200);
  const capture = await captured.json() as any;
  assert.equal(capture.amountMinor, quote.body.fare.minor);
  assert.equal(capture.currency, quote.body.fare.currency);

  const receipt = await get(`/api/rider/trips/${id}/receipt`);
  assert.equal(receipt.status, 200);
  assert.equal(receipt.body.payment.status, 'captured');
  assert.equal(receipt.body.payment.message, 'Payment captured.');

  const receiptPage = await page(RIDER, `/trips/${id}/receipt`);
  assert.equal(receiptPage.status, 200);
  assert.equal(receiptPage.html.includes('Payment captured.'), true);

  await waitFor(async () => {
    const response = await fetch(`${analyticsUrl}/activity/trips/${id}`);
    if (response.status !== 200) return false;
    const activity = await response.json() as any;
    return activity.version === 4 && activity.state === 'completed';
  }, 'completed analytics projection');
  const summaryAfter = await (await fetch(`${analyticsUrl}/activity/summary`)).json() as any;
  const completedAfter = summaryAfter.states
    .find((entry: any) => entry.state === 'completed')?.trips ?? 0;
  assert.equal(summaryAfter.totalTrips, summaryBefore.totalTrips + 1);
  assert.equal(completedAfter, completedBefore + 1);
});

test('a captured referral earns two credits and one funds a later trip', async () => {
  covers('referrals/rewards', 'known-code-is-attributed', 'e2e', 'example', 'direct');
  covers('referrals/rewards', 'no-reward-before-capture', 'e2e', 'example', 'direct');
  covers('referrals/rewards', 'first-capture-awards-pair', 'e2e', 'example', 'direct');
  covers('referrals/rewards', 'owned-credit-reduces-capture', 'e2e', 'example', 'relational');
  covers('referrals/rewards', 'referral-summary-explains-state', 'e2e', 'example', 'direct');

  const suffix = Date.now();
  const inviter = `rider-${suffix}-inviter`;
  const invitee = `rider-${suffix}-invitee`;
  const inviterBefore = await referralSummary(inviter);
  assert.equal(inviterBefore.attributionStatus, 'none');
  assert.deepEqual(inviterBefore.credits, []);

  const qualifying = await requestedReferralTrip(invitee, {
    referralCode: inviterBefore.referralCode,
  });
  const inviteeBeforeCapture = await referralSummary(invitee);
  assert.equal(inviteeBeforeCapture.attributionStatus, 'pending');
  assert.deepEqual(inviteeBeforeCapture.credits, []);
  assert.deepEqual((await referralSummary(inviter)).credits, []);

  await completeTrip(qualifying.id);
  await waitFor(async () => {
    const inviterState = await referralSummary(inviter);
    const inviteeState = await referralSummary(invitee);
    return inviterState.credits.length === 1
      && inviterState.credits[0].status === 'available'
      && inviteeState.attributionStatus === 'qualified'
      && inviteeState.credits.length === 1
      && inviteeState.credits[0].status === 'available';
  }, 'referral reward pair');

  const inviterReward = (await referralSummary(inviter)).credits[0];
  assert.equal(inviterReward.amountMinor, 500);
  assert.equal(inviterReward.currency, qualifying.currency);

  const redeemed = await requestedReferralTrip(inviter, {
    referralCreditId: inviterReward.id,
  });
  await completeTrip(redeemed.id);
  await waitFor(async () => {
    const receipt = await get(`/api/rider/trips/${redeemed.id}/receipt`);
    return receipt.status === 200
      && receipt.body.payment.status === 'captured'
      && receipt.body.payment.adjustment?.kind === 'referral-credit';
  }, 'referral-adjusted receipt');

  const receipt = await get(`/api/rider/trips/${redeemed.id}/receipt`);
  assert.equal(receipt.body.payment.originalFareMinor, redeemed.fareMinor);
  assert.equal(receipt.body.payment.adjustment.deltaMinor, -inviterReward.amountMinor);
  assert.equal(receipt.body.payment.adjustment.creditId, inviterReward.id);
  assert.equal(
    receipt.body.payment.amountMinor,
    redeemed.fareMinor - inviterReward.amountMinor,
  );

  await waitFor(async () =>
    (await referralSummary(inviter)).credits.some(
      (credit) => credit.id === inviterReward.id && credit.status === 'used',
    ), 'used referral credit');

  const receiptPage = await page(RIDER, `/trips/${redeemed.id}/receipt`);
  assert.equal(receiptPage.status, 200);
  assert.equal(receiptPage.html.includes('Original fare'), true);
  assert.equal(receiptPage.html.includes('Referral credit'), true);
  assert.equal(receiptPage.html.includes('Captured total'), true);

  const referralPage = await page(RIDER, `/referrals/${encodeURIComponent(inviter)}`);
  assert.equal(referralPage.status, 200);
  assert.equal(referralPage.html.includes('Referral rewards'), true);
  assert.equal(referralPage.html.includes(inviterReward.currency), true);
  assert.equal(referralPage.html.includes('used'), true);
});

test('referral credit history is grouped by state', async () => {
  const suffix = Date.now();
  const inviter = `rider-${suffix}-filter-inviter`;
  const invitee = `rider-${suffix}-filter-invitee`;
  const inviterBefore = await referralSummary(inviter);
  const qualifying = await requestedReferralTrip(invitee, {
    referralCode: inviterBefore.referralCode,
  });

  await completeTrip(qualifying.id);
  await waitFor(async () => {
    const inviterState = await referralSummary(inviter);
    return inviterState.credits.length === 1
      && inviterState.credits[0].status === 'available';
  }, 'credit-group fixture reward');

  const inviterReward = (await referralSummary(inviter)).credits[0];
  const redeemed = await requestedReferralTrip(inviter, {
    referralCreditId: inviterReward.id,
  });
  await completeTrip(redeemed.id);
  await waitFor(async () =>
    (await referralSummary(inviter)).credits.some(
      (credit) => credit.id === inviterReward.id && credit.status === 'used',
    ), 'credit-group fixture redemption');

  const creditGroups = await page(RIDER, `/referrals/${encodeURIComponent(inviter)}`);
  assert.equal(creditGroups.status, 200);
  assert.equal(creditGroups.html.includes('id="credit-group-available"'), true);
  assert.equal(creditGroups.html.includes('id="credit-group-reserved"'), true);
  assert.equal(creditGroups.html.includes('id="credit-group-used"'), true);
  assert.equal(creditGroups.html.includes('aria-label="Used referral credits"'), true);
  const visibleText = creditGroups.html.replace(/<!--.*?-->/g, '');
  assert.equal(visibleText.includes('No available referral credits.'), true);
  assert.equal(visibleText.includes('No reserved referral credits.'), true);
  assert.equal(creditGroups.html.includes('<strong>used</strong>'), true);
});

test('the stack is reachable', () => {
  assert.ok(trip);
  assert.ok(pricing);
  assert.ok(payments);
  assert.ok(analytics);
  assert.ok(riderWeb);
  assert.ok(driverWeb);
});
