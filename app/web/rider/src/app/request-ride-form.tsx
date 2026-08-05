'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import type { RiderQuote } from '@/lib/view';

/**
 * Quote, then confirm. The two steps are the domain's, not the form's: a quote is a thing the
 * rider is shown and may decline, and the trip is admitted against that quote's id.
 */
export function RequestRideForm() {
  const router = useRouter();
  const [quote, setQuote] = useState<RiderQuote | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [rider] = useState(() => `rider-${Math.random().toString(36).slice(2, 10)}`);

  async function getQuote(form: FormData) {
    setBusy(true);
    setError(null);
    try {
      const response = await fetch('/api/rider/quotes', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({
          pickup: String(form.get('pickup') ?? ''),
          dropoff: String(form.get('dropoff') ?? ''),
          baseMinor: Number(form.get('baseMinor')),
          distanceMinor: Number(form.get('distanceMinor')),
          currency: String(form.get('currency') ?? 'EUR'),
        }),
      });

      const body = await response.json();
      if (!response.ok) {
        // The service refuses with a ProblemDetails; its errorCode is the stable half.
        setError(body?.detail ?? body?.errorCode ?? 'that journey could not be quoted');
        setQuote(null);
        return;
      }

      setQuote(body as RiderQuote);
    } catch {
      setError('the service is unreachable');
    } finally {
      setBusy(false);
    }
  }

  async function confirm() {
    if (!quote) return;
    setBusy(true);
    setError(null);
    try {
      const response = await fetch('/api/rider/trips', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ riderId: rider, quoteId: quote.id }),
      });

      const body = await response.json();
      if (!response.ok) {
        setError(body?.detail ?? body?.errorCode ?? 'that request was refused');
        return;
      }

      router.push(`/trips/${body.id}`);
    } catch {
      setError('the service is unreachable');
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <form className="card" action={getQuote}>
        <h2>Where to?</h2>
        <div className="row">
          <div className="field">
            <label htmlFor="pickup">Pickup</label>
            <input id="pickup" name="pickup" defaultValue="a" required />
          </div>
          <div className="field">
            <label htmlFor="dropoff">Dropoff</label>
            <input id="dropoff" name="dropoff" defaultValue="b" required />
          </div>
        </div>
        <div className="row">
          <div className="field">
            <label htmlFor="baseMinor">Base fare (minor units)</label>
            <input id="baseMinor" name="baseMinor" type="number" defaultValue={500} required />
          </div>
          <div className="field">
            <label htmlFor="distanceMinor">Distance fare (minor units)</label>
            <input id="distanceMinor" name="distanceMinor" type="number" defaultValue={1000} required />
          </div>
        </div>
        <div className="field">
          <label htmlFor="currency">Currency</label>
          <select id="currency" name="currency" defaultValue="EUR">
            <option value="EUR">EUR</option>
            <option value="USD">USD</option>
            <option value="JPY">JPY</option>
          </select>
        </div>
        <button type="submit" disabled={busy}>
          {busy ? 'Working…' : 'Get a quote'}
        </button>
        {error ? <p className="error">{error}</p> : null}
      </form>

      {quote ? (
        <div className="card">
          <h2>Your quote</h2>
          <dl>
            {quote.breakdown.map((component) => (
              <div className="stat" key={component.label}>
                <dt>{component.label}</dt>
                <dd>{component.amountMinor}</dd>
              </div>
            ))}
            <div className="stat">
              <dt>Total</dt>
              <dd>
                {quote.fare.minor} {quote.fare.currency}
              </dd>
            </div>
          </dl>
          <p className="muted" style={{ fontSize: '0.8125rem', margin: '0.75rem 0 1rem' }}>
            Riding as <span className="mono">{rider}</span>. Quote expires{' '}
            {new Date(quote.expiresAt).toLocaleTimeString()}.
          </p>
          <button type="button" onClick={confirm} disabled={busy}>
            {busy ? 'Requesting…' : 'Request this ride'}
          </button>
        </div>
      ) : null}
    </>
  );
}
