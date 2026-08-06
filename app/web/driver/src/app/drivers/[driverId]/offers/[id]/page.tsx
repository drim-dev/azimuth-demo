import { realizes } from '@azimuth/annotations';
import { notFound } from 'next/navigation';
import { acceptOffer } from '@/app/actions';
import { offer as fetchOffer } from '@/lib/trip-service';

export const dynamic = 'force-dynamic';

/**
 * The offer a driver is deciding on — a driver-reachable site on the driver-view claims.
 *
 * It renders what the projection returned. Before accepting there is no rider on this page at all,
 * which is what `rider-contact-hidden-on-offer` asks for; the page has no path to one, because the
 * projection never handed it one.
 */
export default async function OfferPage({
  params,
  searchParams,
}: {
  params: Promise<{ driverId: string; id: string }>;
  searchParams: Promise<{ taken?: string }>;
}) {
  realizes('trips/driver-view', 'pickup-shown-on-offer');
  realizes('trips/driver-view', 'rider-contact-hidden-on-offer');
  realizes('trips/driver-view', 'rider-contact-confined-to-held-trips');

  const { driverId, id } = await params;
  const { taken } = await searchParams;

  const result = await fetchOffer(driverId, id);
  if (result.status !== 200 || !result.body) {
    notFound();
  }

  const offer = result.body;

  return (
    <>
      <h1>Offer</h1>
      <p className="lede">
        <span className="mono">{offer.tripId}</span>
      </p>

      <div className="card">
        <dl>
          <div className="stat">
            <dt>Pickup area</dt>
            <dd>{offer.pickup}</dd>
          </div>
          <div className="stat">
            <dt>Fare</dt>
            <dd>
              {offer.fare.minor} {offer.fare.currency}
            </dd>
          </div>
          <div className="stat">
            <dt>Rider</dt>
            <dd className="muted">Shown once you hold the trip</dd>
          </div>
        </dl>

        <form action={acceptOffer} style={{ marginTop: '1rem' }}>
          <input type="hidden" name="driverId" value={driverId} />
          <input type="hidden" name="tripId" value={offer.tripId} />
          <button type="submit">Accept this offer</button>
        </form>

        {taken ? <p className="error">Another driver took this trip first.</p> : null}
      </div>
    </>
  );
}
