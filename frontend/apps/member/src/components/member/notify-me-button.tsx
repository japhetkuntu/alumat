"use client";

import { Bell } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { usePushNotifications } from "@/hooks/use-push-notifications";

/**
 * The action for an empty list that only fills up when the institution posts
 * something: instead of a dead end, offer to tell the member when it happens.
 * Renders nothing when push is unsupported, already on, or blocked in the
 * browser (a blocked permission can't be re-requested from the page).
 */
export function NotifyMeButton() {
  const { isSupported, permission, isSubscribed, isBusy, subscribe } = usePushNotifications();
  if (!isSupported || permission !== "default" || isSubscribed) return null;

  return (
    <Button variant="outline" size="sm" className="font-semibold gap-1.5" onClick={() => void subscribe().catch(() => {})} disabled={isBusy}>
      <Bell size={13} /> Turn on notifications
    </Button>
  );
}
