import { realizes } from '@azimuth/annotations';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { trip as fetchTrip } from '@/lib/trip-service';
import { Refresher } from './refresher';

export const dynamic = 'force-dynamic';

const LIVE = new Set(['requested', 'assigned', 'in-progress']);
const TERMINAL = new Set(['completed', 'cancelled']);

/**
 * The live trip page — a rider-reachable site on the rider-view claims (D16.2).
 *
 * It renders what the projection returned and decides nothing. A page that re-decided visibility
 * would be another place the rule has to hold, and `position-confined-to-live-phases` quantifies
 * over the whole class of these sites rather than over any one of them.
 */
export default async function TripPage({ params }: { params: Promise<{ id: string }> }) {
  realizes('trip/rider-view', 'no-driver-identity-before-assignment');
  realizes('trip/rider-view', 'no-driver-position-before-assignment');
  realizes('trip/rider-view', 'supply-density-shown-before-assignment');
  realizes('trip/rider-view', 'driver-shown-after-assignment');
  realizes('trip/rider-view', 'driver-position-follows-driver');
  realizes('trip/rider-view', 'no-position-after-completion');
  realizes('trip/rider-view', 'no-position-after-cancellation');
  realizes('trip/rider-view', 'position-confined-to-live-phases');

  const { id } = await params;
  const result = await fetchTrip(id);
  if (result.status !== 200 || !result.body) {
    notFound();
  }

  const trip = result.body;

  return (
    <>
      <Refresher live={LIVE.has(trip.state)} />

      <h1>Your trip</h1>
      <p className="lede">
        <span className="badge" data-state={trip.state}>
          {trip.state}
        </span>{' '}
        <span className="mono">{trip.id}</span>
      </p>

      <div className="card">
        <dl>
          <div className="stat">
            <dt>Fare</dt>
            <dd>
              {trip.fare.minor} {trip.fare.currency}
            </dd>
          </div>
          {trip.driver ? (
            <>
              <div className="stat">
                <dt>Driver</dt>
                <dd>{trip.driver.name}</dd>
              </div>
              <div className="stat">
                <dt>Vehicle</dt>
                <dd>{trip.driver.vehicle ?? '—'}</dd>
              </div>
            </>
          ) : (
            <div className="stat">
              <dt>Driver</dt>
              <dd className="muted">Finding you a driver…</dd>
            </div>
          )}
          {trip.supplyDensity ? (
            <div className="stat">
              <dt>Nearby supply</dt>
              <dd>{trip.supplyDensity}</dd>
            </div>
          ) : null}
        </dl>
      </div>

      {trip.driverPosition ? (
        <div className="card">
          <h2>On the way</h2>
          <p className="muted" style={{ margin: 0 }}>
            Your driver is moving toward you.
          </p>
          <p className="thumb" data-at={trip.driverPosition}>
            Live position: <span className="mono">{trip.driverPosition}</span>
          </p>
        </div>
      ) : null}

      {TERMINAL.has(trip.state) ? (
        <p>
          <Link href={`/trips/${trip.id}/receipt`}>View your receipt</Link>
        </p>
      ) : null}
    </>
  );
}
