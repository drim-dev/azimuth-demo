import { realizes } from '@azimuth/annotations';
import { NextResponse } from 'next/server';
import { referrals } from '@/lib/trip-service';

export const dynamic = 'force-dynamic';

export async function GET(
  _request: Request,
  { params }: { params: Promise<{ riderId: string }> },
) {
  realizes('referrals/rewards', 'referral-summary-explains-state');
  realizes('trips/rider-view', 'position-confined-to-live-phases');

  const { riderId } = await params;

  try {
    const result = await referrals(riderId);
    return NextResponse.json(result.body, { status: result.status });
  } catch (error) {
    return NextResponse.json(
      { error: 'trip-service-unreachable', detail: String(error) },
      { status: 502 },
    );
  }
}
