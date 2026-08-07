import { realizes } from '@azimuth/annotations';
import { NextResponse } from 'next/server';
import { trip } from '@/lib/trip-service';

export const dynamic = 'force-dynamic';

export async function GET(_request: Request, { params }: { params: Promise<{ id: string }> }) {
  // Forwards the trip service's rider projection verbatim. The position reaches this route
  // only in the phases the projection permits, and this route adds nothing of its own.
  realizes('trips/rider-view', 'position-confined-to-live-phases');

  const { id } = await params;

  try {
    const result = await trip(id);
    return NextResponse.json(result.body, { status: result.status });
  } catch (error) {
    return NextResponse.json(
      { error: 'trip-service-unreachable', detail: String(error) },
      { status: 502 },
    );
  }
}
