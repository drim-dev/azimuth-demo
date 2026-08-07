import { realizes } from '@azimuth/annotations';
import { NextResponse } from 'next/server';
import { offer } from '@/lib/trip-service';

export const dynamic = 'force-dynamic';

export async function GET(
  _request: Request,
  { params }: { params: Promise<{ driverId: string; id: string }> },
) {
  const { driverId, id } = await params;

  // An offer is pre-acceptance: the projection behind it carries the pickup and no rider
  // identity or contact, and this route forwards it unchanged.
  realizes('trips/driver-view', 'rider-contact-confined-to-held-trips');

  try {
    const result = await offer(driverId, id);
    return NextResponse.json(result.body, { status: result.status });
  } catch (error) {
    return NextResponse.json(
      { error: 'trip-service-unreachable', detail: String(error) },
      { status: 502 },
    );
  }
}
