import {
  DriverOffer,
  DriverTrip,
  ServiceDriverTrip,
  ServiceOffer,
  driverOffer,
  driverTrip,
} from './view';

const tripUrl = () => process.env.TRIP_URL ?? 'http://localhost:5080';

export type Forwarded<T> = { status: number; body: T | null };

async function forward<T>(path: string, init?: RequestInit): Promise<Forwarded<T>> {
  const response = await fetch(`${tripUrl()}${path}`, { ...init, cache: 'no-store' });
  const text = await response.text();
  return { status: response.status, body: text.length > 0 ? (JSON.parse(text) as T) : null };
}

export async function offer(driverId: string, id: string): Promise<Forwarded<DriverOffer>> {
  const result = await forward<ServiceOffer>(`/drivers/${driverId}/offers/${id}`);
  return result.status === 200 && result.body
    ? { status: 200, body: driverOffer(result.body) }
    : (result as unknown as Forwarded<DriverOffer>);
}

export async function trip(driverId: string, id: string): Promise<Forwarded<DriverTrip>> {
  const result = await forward<ServiceDriverTrip>(`/drivers/${driverId}/trips/${id}`);
  return result.status === 200 && result.body
    ? { status: 200, body: driverTrip(result.body) }
    : (result as unknown as Forwarded<DriverTrip>);
}

/** Claims an offer. The service settles who won; this only reports what it said. */
export async function accept(driverId: string, id: string): Promise<Forwarded<unknown>> {
  return forward(`/trips/${id}/accept/${driverId}`, { method: 'POST' });
}

export async function move(id: string, verb: string, actor: string): Promise<Forwarded<unknown>> {
  return forward(`/trips/${id}/${verb}?actor=${encodeURIComponent(actor)}`, { method: 'POST' });
}
