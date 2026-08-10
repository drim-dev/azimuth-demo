import { realizes } from '@azimuth/annotations';
import { NextResponse } from 'next/server';
import { requestRide } from '@/lib/trip-service';

export const dynamic = 'force-dynamic';

export async function POST(request: Request) {
  realizes('trips/request', 'request-admitted-with-valid-quote');
  realizes('trips/request', 'request-rejected-with-expired-quote');
  realizes('referrals/rewards', 'known-code-is-attributed');
  realizes('referrals/rewards', 'unknown-code-is-rejected');
  realizes('referrals/rewards', 'self-referral-is-rejected');
  realizes('referrals/rewards', 'attribution-cannot-be-replaced');
  realizes('referrals/rewards', 'owned-credit-reduces-capture');
  realizes('referrals/rewards', 'unavailable-credit-is-rejected');
  // Creation answers with the trip's own facts; the response model carries no driver at all,
  // assigned or otherwise.
  realizes('trips/rider-view', 'position-confined-to-live-phases');

  try {
    const result = await requestRide(await request.json());
    return NextResponse.json(result.body, { status: result.status });
  } catch (error) {
    return NextResponse.json(
      { error: 'trip-service-unreachable', detail: String(error) },
      { status: 502 },
    );
  }
}
