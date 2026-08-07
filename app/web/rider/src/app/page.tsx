import { realizes } from '@azimuth/annotations';
import { RequestRideForm } from './request-ride-form';

export const dynamic = 'force-dynamic';

export default function Home() {
  // The request form. It submits a quote id and renders nothing about any driver.
  realizes('trips/rider-view', 'position-confined-to-live-phases');

  return (
    <>
      <h1>Request a ride</h1>
      <p className="lede">
        A quote is priced in whole minor units and spent by at most one request.
      </p>
      <RequestRideForm />
    </>
  );
}
