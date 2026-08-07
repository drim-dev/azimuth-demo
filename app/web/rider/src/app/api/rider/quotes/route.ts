import { realizes } from '@azimuth/annotations';
import { NextResponse } from 'next/server';
import { quote } from '@/lib/trip-service';

export const dynamic = 'force-dynamic';

export async function POST(request: Request) {
  // A quote names a journey and a price. No driver is chosen yet, so there is no position
  // to expose and nothing to route through the projection.
  realizes('trips/rider-view', 'position-confined-to-live-phases');

  try {
    const result = await quote(await request.json());
    return NextResponse.json(result.body, { status: result.status });
  } catch (error) {
    return NextResponse.json(
      { error: 'trip-service-unreachable', detail: String(error) },
      { status: 502 },
    );
  }
}
