import { NextResponse } from 'next/server';
import { trip } from '@/lib/trip-service';

export const dynamic = 'force-dynamic';

export async function GET(
  _request: Request,
  { params }: { params: Promise<{ driverId: string; id: string }> },
) {
  const { driverId, id } = await params;

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
