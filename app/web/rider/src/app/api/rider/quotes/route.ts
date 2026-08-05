import { NextResponse } from 'next/server';
import { quote } from '@/lib/trip-service';

export const dynamic = 'force-dynamic';

export async function POST(request: Request) {
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
