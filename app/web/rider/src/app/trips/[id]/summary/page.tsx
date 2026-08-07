import { realizes } from '@azimuth/annotations';
import { notFound } from 'next/navigation';
import { trip as fetchTrip } from '@/lib/trip-service';

export const dynamic = 'force-dynamic';

export default async function TripSummaryPage({ params }: { params: Promise<{ id: string }> }) {
  realizes('trips/rider-view', 'no-driver-identity-before-assignment');
  realizes('trips/rider-view', 'no-driver-position-before-assignment');
  realizes('trips/rider-view', 'no-position-after-completion');
  realizes('trips/rider-view', 'no-position-after-cancellation');
  realizes('trips/rider-view', 'position-confined-to-live-phases');

  const { id } = await params;
  const result = await fetchTrip(id);
  if (result.status !== 200 || !result.body) {
    notFound();
  }

  const trip = result.body;

  return (
    <>
      <h1>Trip summary</h1>
      <div className="card">
        <dl>
          <div className="stat">
            <dt>Trip</dt>
            <dd className="mono">{trip.id}</dd>
          </div>
          <div className="stat">
            <dt>Status</dt>
            <dd>{trip.state}</dd>
          </div>
          <div className="stat">
            <dt>Quoted fare</dt>
            <dd>
              {trip.fare.minor} {trip.fare.currency}
            </dd>
          </div>
        </dl>
      </div>
    </>
  );
}
