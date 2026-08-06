import { realizes } from '@azimuth/annotations';
import { NextResponse } from 'next/server';
import { requestRide } from '@/lib/trip-service';

export const dynamic = 'force-dynamic';

export async function POST(request: Request) {
  realizes('trips/request', 'request-admitted-with-valid-quote');
  realizes('trips/request', 'request-rejected-with-expired-quote');

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
