import * as http from 'node:http';
import { realizes } from '@azimuth/annotations';
import {
  riderQuote,
  riderReceipt,
  riderRequestAccepted,
  riderTrip,
  ServiceQuote,
  ServiceTrip,
} from './view';

// Read per call rather than at import: a module-level capture makes the service address a property
// of when the file was loaded, which is exactly the kind of thing an e2e harness gets wrong once.
const tripUrl = () => process.env.TRIP_URL ?? 'http://localhost:5080';
const PORT = Number(process.env.PORT ?? 5090);

async function forward(path: string, init?: RequestInit): Promise<[number, unknown]> {
  const response = await fetch(`${tripUrl()}${path}`, init);
  const text = await response.text();
  return [response.status, text.length > 0 ? JSON.parse(text) : null];
}

async function readJson(request: http.IncomingMessage): Promise<Record<string, unknown>> {
  const chunks: Buffer[] = [];
  for await (const chunk of request) chunks.push(chunk as Buffer);
  const body = Buffer.concat(chunks).toString('utf8');
  return body.length > 0 ? JSON.parse(body) : {};
}

/**
 * The rider-facing surface.
 *
 * Every route is a projection of what the trip service returned. The BFF holds no rules of its own:
 * a rule decided in two places is a rule that will disagree in one of them.
 */
export const server = http.createServer(async (request, response) => {
  const url = new URL(request.url ?? '/', 'http://bff');
  const send = (status: number, body: unknown) => {
    response.writeHead(status, { 'content-type': 'application/json' });
    response.end(JSON.stringify(body));
  };

  try {
    if (request.method === 'POST' && url.pathname === '/rider/quotes') {
      const body = await readJson(request);
      const [status, quote] = await forward('/quotes', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify(body),
      });
      return send(status, status === 200 ? riderQuote(quote as ServiceQuote) : quote);
    }

    if (request.method === 'POST' && url.pathname === '/rider/trips') {
      realizes('trip/request', 'request-admitted-with-valid-quote');
      realizes('trip/request', 'request-rejected-with-expired-quote');
      const body = await readJson(request);
      const [status, created] = await forward('/trips', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify(body),
      });
      return send(
        status,
        status === 200
          ? riderRequestAccepted(created as { tripId: string; fareMinor: number; currency: string })
          : created,
      );
    }

    const receipt = url.pathname.match(/^\/rider\/trips\/([0-9a-f-]{36})\/receipt$/i);
    if (request.method === 'GET' && receipt) {
      const [status, body] = await forward(`/trips/${receipt[1]}`);
      if (status !== 200) return send(status, body);
      // The rider view projects the position away once a trip is terminal, and the receipt's map
      // thumbnail needs coordinates, so the driver record is fetched directly.
      const [, driver] = await forward(`/trips/${receipt[1]}/driver`);
      return send(200, riderReceipt(body as ServiceTrip, driver as { position: string | null } | null));
    }

    const trip = url.pathname.match(/^\/rider\/trips\/([0-9a-f-]{36})$/i);
    if (request.method === 'GET' && trip) {
      const [status, body] = await forward(`/trips/${trip[1]}`);
      return send(status, status === 200 ? riderTrip(body as ServiceTrip) : body);
    }

    return send(404, { error: 'no-such-route' });
  } catch (error) {
    return send(502, { error: 'trip-service-unreachable', detail: String(error) });
  }
});

if (require.main === module) {
  server.listen(PORT, () => console.error(`rider bff on ${PORT}, trip at ${tripUrl()}`));
}
