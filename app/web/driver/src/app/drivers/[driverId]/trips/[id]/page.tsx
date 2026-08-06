import { realizes } from '@azimuth/annotations';
import { notFound } from 'next/navigation';
import { moveTrip } from '@/app/actions';
import { trip as fetchTrip } from '@/lib/trip-service';

export const dynamic = 'force-dynamic';

const MOVES: Record<string, { verb: string; label: string }[]> = {
  assigned: [
    { verb: 'start', label: 'Start the trip' },
    { verb: 'cancel', label: 'Cancel' },
  ],
  'in-progress': [
    { verb: 'complete', label: 'Complete the trip' },
    { verb: 'cancel', label: 'Cancel' },
  ],
};

/**
 * The trip a driver holds — the site where a rider contact may appear, and only here.
 *
 * The contact rendered below is whatever the service chose to include. This page never asks for one
 * and could not obtain one if it did: `contact-withdrawn-after-terminal` holds because the
 * projection stops sending it, not because this page remembers to stop showing it.
 */
export default async function DriverTripPage({
  params,
}: {
  params: Promise<{ driverId: string; id: string }>;
}) {
  realizes('trips/driver-view', 'proxy-contact-while-held');
  realizes('trips/driver-view', 'contact-withdrawn-after-terminal');
  realizes('trips/driver-view', 'rider-contact-confined-to-held-trips');

  const { driverId, id } = await params;

  const result = await fetchTrip(driverId, id);
  if (result.status !== 200 || !result.body) {
    notFound();
  }

  const trip = result.body;
  const moves = MOVES[trip.state] ?? [];

  return (
    <>
      <h1>Your trip</h1>
      <p className="lede">
        <span className="badge" data-state={trip.state}>
          {trip.state}
        </span>{' '}
        <span className="mono">{trip.tripId}</span>
      </p>

      <div className="card">
        <dl>
          <div className="stat">
            <dt>Pickup area</dt>
            <dd>{trip.pickup}</dd>
          </div>
          <div className="stat">
            <dt>Fare</dt>
            <dd>
              {trip.fare.minor} {trip.fare.currency}
            </dd>
          </div>
          <div className="stat">
            <dt>Rider contact</dt>
            <dd>
              {trip.riderContact ? (
                <span className="mono">{trip.riderContact}</span>
              ) : (
                <span className="muted">Not available</span>
              )}
            </dd>
          </div>
        </dl>
      </div>

      {moves.length > 0 ? (
        <div className="card">
          <h2>Move the trip</h2>
          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            {moves.map((move) => (
              <form action={moveTrip} key={move.verb}>
                <input type="hidden" name="driverId" value={driverId} />
                <input type="hidden" name="tripId" value={trip.tripId} />
                <input type="hidden" name="verb" value={move.verb} />
                <button type="submit" className={move.verb === 'cancel' ? 'secondary' : undefined}>
                  {move.label}
                </button>
              </form>
            ))}
          </div>
        </div>
      ) : null}
    </>
  );
}
