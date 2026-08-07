import { realizes } from '@azimuth/annotations';
import { redirect } from 'next/navigation';

export const dynamic = 'force-dynamic';

async function open(formData: FormData) {
  'use server';
  const driverId = String(formData.get('driverId') ?? '').trim();
  const tripId = String(formData.get('tripId') ?? '').trim();
  redirect(`/drivers/${driverId}/offers/${tripId}`);
}

/**
 * The driver's way in.
 *
 * A real driver app opens on an inbox of offers. The trip service has no endpoint that lists the
 * offers standing against a driver — only `/trips/{id}/offers`, which is keyed by trip — so the
 * inbox is not built here rather than inventing backend surface no claim asks for. A driver opens a
 * specific offer instead.
 */
export default function Home() {
  // A form that takes two identifiers and redirects. It shows no rider and holds no trip.
  realizes('trips/driver-view', 'rider-contact-confined-to-held-trips');

  return (
    <>
      <h1>Open an offer</h1>
      <p className="lede">
        A driver sees the pickup area and the fare before accepting, and no rider.
      </p>

      <form className="card" action={open}>
        <div className="field">
          <label htmlFor="driverId">Your driver id</label>
          <input id="driverId" name="driverId" defaultValue="driver-0" required />
        </div>
        <div className="field">
          <label htmlFor="tripId">Trip id</label>
          <input id="tripId" name="tripId" placeholder="0000000000000" required />
        </div>
        <button type="submit">Open offer</button>
      </form>
    </>
  );
}
