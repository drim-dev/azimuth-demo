import { realizes } from '@azimuth/annotations';
import { NextResponse } from 'next/server';
import { trip } from '@/lib/trip-service';

export const dynamic = 'force-dynamic';

export async function GET(
  _request: Request,
  { params }: { params: Promise<{ driverId: string; id: string }> },
) {
  const { driverId, id } = await params;

  // Forwards the trip service's driver projection, which reveals the rider's proxy contact
  // only while this driver holds the trip. This route adds nothing and filters nothing.
  realizes('trips/driver-view', 'rider-contact-confined-to-held-trips');

  try {
    const result = await trip(driverId, id);
    return NextResponse.json(result.body, { status: result.status });
  } catch (error) {
    return NextResponse.json(
      { error: 'trip-service-unreachable', detail: String(error) },
      { status: 502 },
    );
  }
}
