import {
  RiderQuote,
  RiderReceipt,
  RiderTrip,
  ServicePaymentStatus,
  ServiceQuote,
  ServiceTrip,
  riderQuote,
  riderReceipt,
  riderRequestAccepted,
  riderTrip,
} from './view';

/**
 * The rider BFF's only door to the trip service.
 *
 * Read per call rather than at module load: a module-level capture makes the service address a
 * property of when the file was loaded, which is exactly the kind of thing an e2e harness gets
 * wrong once.
 */
const tripUrl = () => process.env.TRIP_URL ?? 'http://localhost:5080';
const pricingUrl = () => process.env.PRICING_URL ?? 'http://localhost:5070';
const paymentsUrl = () => process.env.PAYMENTS_URL ?? 'http://localhost:5085';

export type Forwarded<T> = { status: number; body: T | null };

async function forward<T>(base: string, path: string, init?: RequestInit): Promise<Forwarded<T>> {
  const response = await fetch(`${base}${path}`, { ...init, cache: 'no-store' });
  const text = await response.text();
  return { status: response.status, body: text.length > 0 ? (JSON.parse(text) as T) : null };
}

export async function quote(body: unknown): Promise<Forwarded<RiderQuote>> {
  const result = await forward<ServiceQuote>(pricingUrl(), '/quotes', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
  });

  return result.status === 200 && result.body
    ? { status: 200, body: riderQuote(result.body) }
    : (result as unknown as Forwarded<RiderQuote>);
}

export async function requestRide(body: unknown) {
  const result = await forward<{ tripId: string; fareMinor: number; currency: string }>(tripUrl(), '/trips', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(body),
  });

  return result.status === 200 && result.body
    ? { status: 200, body: riderRequestAccepted(result.body) }
    : result;
}

export async function trip(id: string): Promise<Forwarded<RiderTrip>> {
  const result = await forward<ServiceTrip>(tripUrl(), `/trips/${id}`);
  return result.status === 200 && result.body
    ? { status: 200, body: riderTrip(result.body) }
    : (result as unknown as Forwarded<RiderTrip>);
}

export async function receipt(id: string): Promise<Forwarded<RiderReceipt>> {
  const result = await forward<ServiceTrip>(tripUrl(), `/trips/${id}`);
  if (result.status !== 200 || !result.body) {
    return result as unknown as Forwarded<RiderReceipt>;
  }

  const driver = await forward<{ display?: string }>(tripUrl(), `/trips/${id}/driver`);
  const payment = await forward<ServicePaymentStatus>(paymentsUrl(), `/captures/${id}/status`);
  return {
    status: 200,
    body: riderReceipt(
      result.body,
      driver.status === 200 ? driver.body : null,
      payment.status === 200 && payment.body
        ? payment.body
        : {
            status: 'unavailable',
            amountMinor: null,
            currency: null,
            message: 'Payment status is temporarily unavailable.',
          },
    ),
  };
}
