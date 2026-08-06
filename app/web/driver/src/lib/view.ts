import { realizes } from '@azimuth/annotations';

/**
 * The driver BFF's view model — the second BFF, and the asymmetry D1 called non-negotiable.
 *
 * It is not the rider BFF with a flag. The two sides have opposite visibility rules: a rider sees
 * the driver only *after* assignment, a driver sees the rider's contact only *while holding* the
 * trip. One shared projection with a direction flag is one conditional away from returning the
 * wrong side's data, so these two apps share no projection code at all. The duplication is the
 * point.
 */

export interface ServiceOffer {
  tripId: string;
  pickupArea: string;
  fareMinor: number;
  currency: string;
}

export interface ServiceDriverTrip {
  tripId: string;
  state: string;
  pickupArea: string;
  fareMinor: number;
  currency: string;
  riderContact: string | null;
}

export interface DriverOffer {
  tripId: string;
  pickup: string;
  fare: { minor: number; currency: string };
}

export interface DriverTrip {
  tripId: string;
  state: string;
  pickup: string;
  fare: { minor: number; currency: string };
  riderContact: string | null;
}

/** What a driver sees before accepting. Carries no rider. */
export function driverOffer(offer: ServiceOffer): DriverOffer {
  realizes('trips/driver-view', 'pickup-shown-on-offer');
  realizes('trips/driver-view', 'rider-contact-hidden-on-offer');
  realizes('trips/driver-view', 'rider-contact-confined-to-held-trips');
  return {
    tripId: offer.tripId,
    pickup: offer.pickupArea,
    fare: { minor: offer.fareMinor, currency: offer.currency },
  };
}

/** What a driver sees for a trip. The contact is whatever the service chose to include. */
export function driverTrip(trip: ServiceDriverTrip): DriverTrip {
  realizes('trips/driver-view', 'proxy-contact-while-held');
  realizes('trips/driver-view', 'contact-withdrawn-after-terminal');
  realizes('trips/driver-view', 'rider-contact-confined-to-held-trips');
  return {
    tripId: trip.tripId,
    state: trip.state,
    pickup: trip.pickupArea,
    fare: { minor: trip.fareMinor, currency: trip.currency },
    riderContact: trip.riderContact ?? null,
  };
}
