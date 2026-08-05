import { RequestRideForm } from './request-ride-form';

export const dynamic = 'force-dynamic';

export default function Home() {
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
