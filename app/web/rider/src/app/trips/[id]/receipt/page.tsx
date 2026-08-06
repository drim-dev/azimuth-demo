import { realizes } from '@azimuth/annotations';
import { notFound } from 'next/navigation';
import { receipt as fetchReceipt } from '@/lib/trip-service';

export const dynamic = 'force-dynamic';

/**
 * The receipt page.
 *
 * The second rider-reachable surface, and the one that leaked. It satisfied every behavioural claim
 * in `trips/rider-view` and showed a completed trip's driver position anyway, because the thumbnail
 * reached past the projection for the stored record. The thumbnail now uses the rider's own pickup
 * and dropoff, which is what it should always have used.
 */
export default async function ReceiptPage({ params }: { params: Promise<{ id: string }> }) {
  realizes('trips/rider-view', 'driver-identity-remains-on-receipt');
  realizes('trips/rider-view', 'position-confined-to-live-phases');

  const { id } = await params;
  const result = await fetchReceipt(id);
  if (result.status !== 200 || !result.body) {
    notFound();
  }

  const receipt = result.body;

  return (
    <>
      <h1>Receipt</h1>
      <p className="lede">
        <span className="badge" data-state={receipt.state}>
          {receipt.state}
        </span>{' '}
        <span className="mono">{receipt.id}</span>
      </p>

      <div className="card">
        <dl>
          <div className="stat">
            <dt>Total</dt>
            <dd>
              {receipt.fare.minor} {receipt.fare.currency}
            </dd>
          </div>
          <div className="stat">
            <dt>Driver</dt>
            <dd>{receipt.driver?.name ?? '—'}</dd>
          </div>
        </dl>

        <p className="thumb">
          {receipt.route.pickup
            ? `Route ${receipt.route.pickup} → ${receipt.route.dropoff}`
            : 'Route thumbnail unavailable for this trip.'}
        </p>
      </div>
    </>
  );
}
