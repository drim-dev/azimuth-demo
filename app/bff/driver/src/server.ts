import * as http from 'node:http';
import { realizes } from '@azimuth/annotations';

/**
 * The driver BFF — the second BFF, and the asymmetry D1 called non-negotiable.
 *
 * It is not the rider BFF with a flag. The two sides have opposite visibility rules, and the one
 * shared projection that served both would be one conditional away from returning the wrong side's
 * data. The duplication is the point.
 */

const tripUrl = () => process.env.TRIP_URL ?? 'http://localhost:5080';
const PORT = Number(process.env.PORT ?? 5095);

async function forward(path: string): Promise<[number, any]> {
  const response = await fetch(`${tripUrl()}${path}`);
  const text = await response.text();
  return [response.status, text.length > 0 ? JSON.parse(text) : null];
}

/** What a driver sees before accepting. Carries no rider. */
export function driverOffer(offer: any) {
  realizes('trip/driver-view', 'pickup-shown-on-offer');
  realizes('trip/driver-view', 'rider-contact-hidden-on-offer');
  realizes('trip/driver-view', 'rider-contact-confined-to-held-trips');
  return {
    tripId: offer.tripId,
    pickup: offer.pickupArea,
    fare: { minor: offer.fareMinor, currency: offer.currency },
  };
}

/** What a driver sees for a trip. The contact is whatever the service chose to include. */
export function driverTrip(trip: any) {
  realizes('trip/driver-view', 'proxy-contact-while-held');
  realizes('trip/driver-view', 'contact-withdrawn-after-terminal');
  realizes('trip/driver-view', 'rider-contact-confined-to-held-trips');
  return {
    tripId: trip.tripId,
    state: trip.state,
    pickup: trip.pickupArea,
    fare: { minor: trip.fareMinor, currency: trip.currency },
    riderContact: trip.riderContact ?? null,
  };
}

export const server = http.createServer(async (request, response) => {
  const url = new URL(request.url ?? '/', 'http://bff');
  const send = (status: number, body: unknown) => {
    response.writeHead(status, { 'content-type': 'application/json' });
    response.end(JSON.stringify(body));
  };

  try {
    const offer = url.pathname.match(/^\/driver\/([\w-]+)\/offers\/([0-9a-f-]{36})$/i);
    if (offer) {
      const [status, body] = await forward(`/drivers/${offer[1]}/offers/${offer[2]}`);
      return send(status, status === 200 ? driverOffer(body) : body);
    }

    const trip = url.pathname.match(/^\/driver\/([\w-]+)\/trips\/([0-9a-f-]{36})$/i);
    if (trip) {
      const [status, body] = await forward(`/drivers/${trip[1]}/trips/${trip[2]}`);
      return send(status, status === 200 ? driverTrip(body) : body);
    }

    return send(404, { error: 'no-such-route' });
  } catch (error) {
    return send(502, { error: 'trip-service-unreachable', detail: String(error) });
  }
});

if (require.main === module) {
  server.listen(PORT, () => console.error(`driver bff on ${PORT}`));
}
