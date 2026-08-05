import { realizes } from '@azimuth/annotations';

/**
 * The rider BFF's view model.
 *
 * The same claims are realized here and in the trip service. That is the fan-out this demo exists
 * to study: one claim, several sites, more than one language — and it is why the verification plan
 * raises two of these claims to `e2e`. Each site can pass in isolation while the composition leaks:
 * the service omits a field, the BFF projects a different model, the client renders a cached value
 * from an earlier poll.
 */

export interface ServiceTrip {
  tripId: string;
  state: string;
  fareMinor: number;
  currency: string;
  driverDisplay: string | null;
  vehicle: string | null;
  driverPosition: string | null;
  supplyDensity: string | null;
}

export interface RiderTrip {
  id: string;
  state: string;
  fare: { minor: number; currency: string };
  driver: { name: string; vehicle: string | null } | null;
  driverPosition: string | null;
  supplyDensity: string | null;
  awaitingDriver: boolean;
}

const ASSIGNED = new Set(['assigned', 'in-progress']);
const TERMINAL = new Set(['completed', 'cancelled']);

/**
 * Re-projects what the service returned, and never widens it.
 *
 * The BFF is deliberately unable to reveal more than the service sent: there is no path from here
 * to a driver's position other than the field the service already decided to include. A BFF that
 * fetched the raw position itself would be a second place the rule has to hold, which is exactly
 * the shape concern C1 warns about.
 */
export function riderTrip(trip: ServiceTrip): RiderTrip {
  realizes('trip/rider-view', 'no-driver-identity-before-assignment');
  realizes('trip/rider-view', 'no-driver-position-before-assignment');
  realizes('trip/rider-view', 'supply-density-shown-before-assignment');
  realizes('trip/rider-view', 'driver-shown-after-assignment');
  realizes('trip/rider-view', 'driver-position-follows-driver');
  realizes('trip/rider-view', 'no-position-after-completion');
  realizes('trip/rider-view', 'no-position-after-cancellation');
  realizes('trip/rider-view', 'driver-identity-remains-on-receipt');

  const assigned = ASSIGNED.has(trip.state);
  const terminal = TERMINAL.has(trip.state);

  return {
    id: trip.tripId,
    state: trip.state,
    fare: { minor: trip.fareMinor, currency: trip.currency },
    driver: trip.driverDisplay ? { name: trip.driverDisplay, vehicle: trip.vehicle } : null,
    driverPosition: assigned ? trip.driverPosition : null,
    supplyDensity: assigned || terminal ? null : trip.supplyDensity,
    awaitingDriver: trip.state === 'requested',
  };
}

export interface ServiceQuote {
  id: string;
  totalMinor: number;
  currency: string;
  expiresAt: string;
  breakdown: { label: string; amountMinor: number }[];
}

export function riderQuote(quote: ServiceQuote) {
  realizes('pricing/quote', 'quote-returned');
  realizes('pricing/quote', 'breakdown-accompanies-quote');
  return {
    id: quote.id,
    fare: { minor: quote.totalMinor, currency: quote.currency },
    expiresAt: quote.expiresAt,
    breakdown: quote.breakdown,
  };
}

export function riderRequestAccepted(body: { tripId: string; fareMinor: number; currency: string }) {
  realizes('trip/request', 'rider-informed-of-trip');
  realizes('trip/request', 'trip-created-in-requested-state');
  return {
    id: body.tripId,
    state: 'requested',
    fare: { minor: body.fareMinor, currency: body.currency },
    awaitingDriver: true,
  };
}


export interface RiderReceipt {
  id: string;
  state: string;
  fare: { minor: number; currency: string };
  driver: { name: string; vehicle: string | null } | null;
  /** Last known pickup/dropoff positions, for the map thumbnail on the receipt. */
  route: { pickup: string | null; dropoff: string | null; lastKnown: string | null };
}

/**
 * A receipt for a finished trip.
 *
 * The driver's display name stays visible on a receipt, which is what
 * `driver-identity-remains-on-receipt` asks for. The map thumbnail needs coordinates.
 */
export function riderReceipt(
  trip: ServiceTrip,
  driver: { position: string | null } | null,
): RiderReceipt {
  realizes('trip/rider-view', 'driver-identity-remains-on-receipt');
  return {
    id: trip.tripId,
    state: trip.state,
    fare: { minor: trip.fareMinor, currency: trip.currency },
    driver: trip.driverDisplay ? { name: trip.driverDisplay, vehicle: trip.vehicle } : null,
    route: { pickup: null, dropoff: null, lastKnown: driver?.position ?? null },
  };
}
