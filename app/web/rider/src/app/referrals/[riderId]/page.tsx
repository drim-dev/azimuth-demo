import { realizes } from '@azimuth/annotations';
import { notFound } from 'next/navigation';
import { referrals } from '@/lib/trip-service';
import type { ReferralCreditStatus } from '@/lib/view';

export const dynamic = 'force-dynamic';

const CREDIT_GROUPS: ReadonlyArray<{ status: ReferralCreditStatus; label: string }> = [
  { status: 'available', label: 'Available' },
  { status: 'reserved', label: 'Reserved' },
  { status: 'used', label: 'Used' },
];

export default async function ReferralSummaryPage(
  { params }: { params: Promise<{ riderId: string }> },
) {
  realizes('referrals/rewards', 'referral-summary-explains-state');
  realizes('trips/rider-view', 'position-confined-to-live-phases');

  const { riderId } = await params;
  const result = await referrals(riderId);
  if (result.status !== 200 || !result.body) {
    notFound();
  }

  const summary = result.body;
  return (
    <>
      <h1>Referral rewards</h1>
      <div className="card">
        <dl>
          <div className="stat">
            <dt>Your referral code</dt>
            <dd className="mono">{summary.referralCode}</dd>
          </div>
          <div className="stat">
            <dt>Referral attribution</dt>
            <dd>{summary.attributionStatus}</dd>
          </div>
        </dl>
        <h2>Credits</h2>
        <div className="credit-groups">
          {CREDIT_GROUPS.map((group) => {
            const credits = summary.credits.filter((credit) => credit.status === group.status);
            return (
              <section key={group.status} aria-labelledby={`credit-group-${group.status}`}>
                <h3 id={`credit-group-${group.status}`}>{group.label} ({credits.length})</h3>
                {credits.length === 0 ? (
                  <p className="muted">No {group.status} referral credits.</p>
                ) : (
                  <ul className="credit-list" aria-label={`${group.label} referral credits`}>
                    {credits.map((credit) => (
                      <li key={credit.id}>
                        <span>{credit.amountMinor} {credit.currency}</span>
                        <strong>{credit.status}</strong>
                      </li>
                    ))}
                  </ul>
                )}
              </section>
            );
          })}
        </div>
      </div>
    </>
  );
}
