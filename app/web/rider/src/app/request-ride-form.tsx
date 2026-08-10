'use client';

import { realizes } from '@azimuth/annotations';
import { useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';
import type { RiderQuote, RiderReferralSummary } from '@/lib/view';

const RIDER_SESSION_KEY = 'azimuth-demo-rider';

type ProblemDetails = {
  detail?: unknown;
  errorCode?: unknown;
  title?: unknown;
};

function problemMessage(body: ProblemDetails | null, fallback: string) {
  if (typeof body?.detail === 'string') return body.detail;
  if (typeof body?.errorCode === 'string') return body.errorCode;
  if (typeof body?.title === 'string') return body.title;
  return fallback;
}

async function responseBody(response: Response): Promise<Record<string, unknown> | null> {
  try {
    return await response.json() as Record<string, unknown>;
  } catch {
    return null;
  }
}

/**
 * Quote, then confirm. The two steps are the domain's, not the form's: a quote is a thing the
 * rider is shown and may decline, and the trip is admitted against that signed quote.
 */
export function RequestRideForm() {
  realizes('referrals/rewards', 'referral-summary-explains-state');

  const router = useRouter();
  const [quote, setQuote] = useState<RiderQuote | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [referralError, setReferralError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [rider, setRider] = useState<string | null>(null);
  const [referralCode, setReferralCode] = useState('');
  const [referralCreditId, setReferralCreditId] = useState('');
  const [referralSummary, setReferralSummary] = useState<RiderReferralSummary | null>(null);

  async function refreshReferralSummary(riderId: string) {
    setReferralError(null);
    try {
      const response = await fetch(`/api/rider/referrals/${encodeURIComponent(riderId)}`, {
        cache: 'no-store',
      });
      const body = await responseBody(response);
      if (!response.ok || !body) {
        setReferralError(problemMessage(body, 'Referral rewards are temporarily unavailable.'));
        return;
      }

      const summary = body as unknown as RiderReferralSummary;
      setReferralSummary(summary);
      setReferralCreditId((selected) =>
        summary.credits.some((credit) => credit.id === selected && credit.status === 'available')
          ? selected
          : '',
      );
    } catch {
      setReferralError('Referral rewards are temporarily unavailable.');
    }
  }

  useEffect(() => {
    let riderId = `rider-${Math.random().toString(36).slice(2, 10)}`;
    try {
      riderId = sessionStorage.getItem(RIDER_SESSION_KEY) ?? riderId;
      sessionStorage.setItem(RIDER_SESSION_KEY, riderId);
    } catch {
      // The fixture can still request a ride when browser storage is disabled; only reload
      // continuity is lost. This identifier is not an authentication boundary.
    }

    setRider(riderId);
    void refreshReferralSummary(riderId);
  }, []);

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
          distanceMeters: Number(form.get('distanceMeters')),
          currency: String(form.get('currency') ?? 'EUR'),
        }),
      });

      const body = await responseBody(response);
      if (!response.ok) {
        setError(problemMessage(body, 'That journey could not be quoted.'));
        setQuote(null);
        return;
      }

      if (!body) {
        setError('The pricing service returned no quote.');
        setQuote(null);
        return;
      }

      setQuote(body as unknown as RiderQuote);
    } catch {
      setError('the service is unreachable');
    } finally {
      setBusy(false);
    }
  }

  async function confirm() {
    if (!quote || !rider) return;
    setBusy(true);
    setError(null);
    try {
      const code = referralCode.trim();
      const response = await fetch('/api/rider/trips', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({
          riderId: rider,
          quoteToken: quote.token,
          ...(code ? { referralCode: code } : {}),
          ...(referralCreditId ? { referralCreditId } : {}),
        }),
      });

      const body = await responseBody(response);
      if (!response.ok) {
        setError(problemMessage(body, 'That request was refused.'));
        return;
      }

      if (typeof body?.id !== 'string') {
        setError('The trip service returned no trip identifier.');
        return;
      }

      await refreshReferralSummary(rider);
      router.push(`/trips/${body.id}`);
    } catch {
      setError('the service is unreachable');
    } finally {
      setBusy(false);
    }
  }

  const availableCredits = referralSummary?.credits.filter(
    (credit) => credit.status === 'available',
  ) ?? [];
  const redeemableCredits = quote
    ? availableCredits.filter((credit) => credit.currency === quote.fare.currency)
    : [];
  const mayAttachReferral = referralSummary?.attributionStatus === 'none';

  return (
    <>
      <section className="card" aria-labelledby="referral-rewards-heading" aria-live="polite">
        <h2 id="referral-rewards-heading">Referral rewards</h2>
        {referralSummary ? (
          <>
          <dl>
            <div className="stat">
              <dt>Your referral code</dt>
              <dd className="mono">{referralSummary.referralCode}</dd>
            </div>
            <div className="stat">
              <dt>Referral attribution</dt>
              <dd>{referralSummary.attributionStatus}</dd>
            </div>
            <div className="stat">
              <dt>Available credits</dt>
              <dd>{availableCredits.length}</dd>
            </div>
            <div className="stat">
              <dt>Reserved credits</dt>
              <dd>{referralSummary.credits.filter((credit) => credit.status === 'reserved').length}</dd>
            </div>
            <div className="stat">
              <dt>Used credits</dt>
              <dd>
                {referralSummary.credits.filter((credit) => credit.status === 'used').length}
              </dd>
            </div>
          </dl>
          {rider ? <a href={`/referrals/${encodeURIComponent(rider)}`}>Open referral summary</a> : null}
          </>
        ) : rider && !referralError ? (
          <p className="muted">Loading referral rewards…</p>
        ) : null}
        <p className="muted fixture-note">
          This demo remembers the rider for this browser tab. It is not an authenticated account.
        </p>
        {referralError ? <p className="error" role="alert">{referralError}</p> : null}
        {referralSummary?.credits.length ? (
          <ul className="credit-list" aria-label="Referral credits">
            {referralSummary.credits.map((credit) => (
              <li key={credit.id}>
                <span>{credit.amountMinor} {credit.currency}</span>
                <span>{credit.status}</span>
              </li>
            ))}
          </ul>
        ) : null}
      </section>

      <form className="card" action={getQuote}>
        <h2>Where to?</h2>
        <div className="row">
          <div className="field">
            <label htmlFor="pickup">Pickup</label>
            <input id="pickup" name="pickup" defaultValue="downtown" required />
          </div>
          <div className="field">
            <label htmlFor="dropoff">Dropoff</label>
            <input id="dropoff" name="dropoff" defaultValue="airport" required />
          </div>
        </div>
        <div className="row">
          <div className="field">
            <label htmlFor="distanceMeters">Estimated distance (metres)</label>
            <input id="distanceMeters" name="distanceMeters" type="number" defaultValue={10000} required />
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
        <div className="field">
          <label htmlFor="referralCode">Referral code (optional)</label>
          <input
            id="referralCode"
            name="referralCode"
            value={referralCode}
            onChange={(event) => setReferralCode(event.target.value)}
            disabled={referralSummary !== null && !mayAttachReferral}
            autoComplete="off"
          />
          {referralSummary !== null && !mayAttachReferral ? (
            <span className="field-help">
              Referral attribution is already {referralSummary.attributionStatus}.
            </span>
          ) : null}
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
            Riding as <span className="mono">{rider ?? 'preparing rider…'}</span>. Quote expires{' '}
            {new Date(quote.expiresAt).toLocaleTimeString()}.
          </p>
          <div className="field">
            <label htmlFor="referralCreditId">Referral credit</label>
            <select
              id="referralCreditId"
              value={referralCreditId}
              onChange={(event) => setReferralCreditId(event.target.value)}
              disabled={redeemableCredits.length === 0}
            >
              <option value="">Do not use a referral credit</option>
              {redeemableCredits.map((credit) => (
                <option key={credit.id} value={credit.id}>
                  Use {credit.amountMinor} {credit.currency}
                </option>
              ))}
            </select>
            {availableCredits.length > 0 && redeemableCredits.length === 0 ? (
              <span className="field-help">
                No available referral credit uses {quote.fare.currency}.
              </span>
            ) : null}
          </div>
          <button type="button" onClick={confirm} disabled={busy || rider === null}>
            {busy ? 'Requesting…' : 'Request this ride'}
          </button>
        </div>
      ) : null}
    </>
  );
}
