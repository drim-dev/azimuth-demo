import { realizes } from '@azimuth/annotations';
import { NextResponse } from 'next/server';
import { receipt } from '@/lib/trip-service';

export const dynamic = 'force-dynamic';

export async function GET(_request: Request, { params }: { params: Promise<{ id: string }> }) {
  // The surface that leaked once. It forwards the receipt projection, which is terminal-phase
  // and therefore carries the driver's name and no position.
  realizes('trips/rider-view', 'position-confined-to-live-phases');
  realizes('payments/capture', 'receipt-explains-payment-state');
  realizes('referrals/rewards', 'owned-credit-reduces-capture');

  const { id } = await params;

  try {
    const result = await receipt(id);
    return NextResponse.json(result.body, { status: result.status });
  } catch (error) {
    return NextResponse.json(
      { error: 'trip-service-unreachable', detail: String(error) },
      { status: 502 },
    );
  }
}
