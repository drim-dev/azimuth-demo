'use client';

import { useRouter } from 'next/navigation';
import { useEffect } from 'react';

/**
 * Re-renders the page on the server while the trip is live.
 *
 * `router.refresh()` rather than client-side state: the projection runs on the server, so what the
 * page shows after a poll is decided by the same function that decided the first render. A client
 * that cached the earlier payload and merged into it is the third failure mode `view.ts` names.
 */
export function Refresher({ live }: { live: boolean }) {
  const router = useRouter();

  useEffect(() => {
    if (!live) return;
    const timer = setInterval(() => router.refresh(), 3000);
    return () => clearInterval(timer);
  }, [live, router]);

  return null;
}
