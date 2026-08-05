import * as http from 'node:http';
import { realizes } from '@azimuth/annotations';

/**
 * The rider web app — the third site on the rider-view claims (D16.2).
 *
 * It renders what the BFF returns and decides nothing. A web app that re-decided visibility would
 * be a third place the rule has to hold.
 */

const bff = () => process.env.BFF_URL ?? 'http://localhost:5090';
const PORT = Number(process.env.PORT ?? 5100);

function escapeHtml(value: unknown): string {
  return String(value ?? '').replace(/[&<>"]/g, (c) =>
    ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c] as string,
  );
}

/** The live trip page. Shows whatever the BFF chose to include, and nothing more. */
function tripPage(trip: any): string {
  realizes('trip/rider-view', 'no-driver-identity-before-assignment');
  realizes('trip/rider-view', 'no-driver-position-before-assignment');
  realizes('trip/rider-view', 'supply-density-shown-before-assignment');
  realizes('trip/rider-view', 'driver-shown-after-assignment');
  realizes('trip/rider-view', 'driver-position-follows-driver');
  realizes('trip/rider-view', 'no-position-after-completion');
  realizes('trip/rider-view', 'no-position-after-cancellation');
  realizes('trip/rider-view', 'position-confined-to-live-phases');

  const driver = trip.driver
    ? `<p>Driver: ${escapeHtml(trip.driver.name)} — ${escapeHtml(trip.driver.vehicle)}</p>`
    : '<p>Finding you a driver…</p>';
  const position = trip.driverPosition
    ? `<p class="map" data-at="${escapeHtml(trip.driverPosition)}">Driver is on the way</p>`
    : '';
  const density = trip.supplyDensity
    ? `<p>Nearby supply: ${escapeHtml(trip.supplyDensity)}</p>`
    : '';
  return `<h1>Trip ${escapeHtml(trip.id)}</h1><p>${escapeHtml(trip.state)}</p>${driver}${position}${density}`;
}

/** The receipt page. */
function receiptPage(receipt: any): string {
  realizes('trip/rider-view', 'driver-identity-remains-on-receipt');
  realizes('trip/rider-view', 'position-confined-to-live-phases');
  const map = receipt.route?.pickup
    ? `<img class="thumb" alt="route" src="/map?from=${encodeURIComponent(receipt.route.pickup)}">`
    : '';
  return `<h1>Receipt</h1>
<p>${escapeHtml(receipt.fare.minor)} ${escapeHtml(receipt.fare.currency)}</p>
<p>Driver: ${escapeHtml(receipt.driver?.name)}</p>${map}`;
}

export const server = http.createServer(async (request, response) => {
  const url = new URL(request.url ?? '/', 'http://web');
  const html = (body: string) => {
    response.writeHead(200, { 'content-type': 'text/html' });
    response.end(`<!doctype html><meta charset="utf-8"><title>Ride</title>${body}`);
  };

  const receipt = url.pathname.match(/^\/trips\/([0-9a-f-]{36})\/receipt$/i);
  if (receipt) {
    const body = await (await fetch(`${bff()}/rider/trips/${receipt[1]}/receipt`)).json();
    return html(receiptPage(body));
  }

  const trip = url.pathname.match(/^\/trips\/([0-9a-f-]{36})$/i);
  if (trip) {
    const body = await (await fetch(`${bff()}/rider/trips/${trip[1]}`)).json();
    return html(tripPage(body));
  }

  response.writeHead(404).end();
});

if (require.main === module) {
  server.listen(PORT, () => console.error(`rider web on ${PORT}`));
}
