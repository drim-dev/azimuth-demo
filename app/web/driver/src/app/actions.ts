'use server';

import { revalidatePath } from 'next/cache';
import { redirect } from 'next/navigation';
import { accept, move } from '@/lib/trip-service';

/**
 * Claiming an offer.
 *
 * The service settles who won by compare-and-set; a loser is told it lost by the status it gets
 * back. Nothing here re-decides that, because a second place deciding assignment is exactly the
 * double-assignment the store's conditional write exists to prevent.
 */
export async function acceptOffer(formData: FormData) {
  const driverId = String(formData.get('driverId') ?? '');
  const tripId = String(formData.get('tripId') ?? '');

  const result = await accept(driverId, tripId);
  if (result.status !== 200) {
    redirect(`/drivers/${driverId}/offers/${tripId}?taken=1`);
  }

  redirect(`/drivers/${driverId}/trips/${tripId}`);
}

export async function moveTrip(formData: FormData) {
  const driverId = String(formData.get('driverId') ?? '');
  const tripId = String(formData.get('tripId') ?? '');
  const verb = String(formData.get('verb') ?? '');

  await move(tripId, verb, driverId);
  revalidatePath(`/drivers/${driverId}/trips/${tripId}`);
}
