"use client";

import { useCallback, useEffect, useState } from "react";
import { useAuth } from "@/hooks/use-auth";
import { deletePushSubscription, getVapidPublicKey, postPushSubscription } from "@/lib/institution-api";

function urlBase64ToUint8Array(base64String: string): ArrayBuffer {
  const padding = "=".repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, "+").replace(/_/g, "/");
  const rawData = atob(base64);
  return Uint8Array.from([...rawData].map((c) => c.charCodeAt(0))).buffer as ArrayBuffer;
}

export function usePushNotifications() {
  const { user } = useAuth();
  const isSupported = typeof window !== "undefined" && "serviceWorker" in navigator && "PushManager" in window;
  const [permission, setPermission] = useState<NotificationPermission>(
    isSupported ? Notification.permission : "denied"
  );
  const [isSubscribed, setIsSubscribed] = useState(false);
  const [isBusy, setIsBusy] = useState(false);

  useEffect(() => {
    if (!isSupported || !user) return;
    let cancelled = false;
    (async () => {
      const reg = await navigator.serviceWorker.getRegistration("/sw.js");
      const sub = await reg?.pushManager.getSubscription();
      if (!cancelled) setIsSubscribed(!!sub);
    })();
    return () => { cancelled = true; };
  }, [isSupported, user]);

  const subscribe = useCallback(async () => {
    if (!isSupported) return;
    setIsBusy(true);
    try {
      const perm = await Notification.requestPermission();
      setPermission(perm);
      if (perm !== "granted") return;

      const reg = await navigator.serviceWorker.register("/sw.js");
      const publicKey = await getVapidPublicKey();
      const sub = await reg.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: urlBase64ToUint8Array(publicKey),
      });
      await postPushSubscription(sub.toJSON());
      setIsSubscribed(true);
    } finally {
      setIsBusy(false);
    }
  }, [isSupported]);

  const unsubscribe = useCallback(async () => {
    if (!isSupported) return;
    setIsBusy(true);
    try {
      const reg = await navigator.serviceWorker.getRegistration("/sw.js");
      const sub = await reg?.pushManager.getSubscription();
      if (sub) {
        await deletePushSubscription({ endpoint: sub.endpoint });
        await sub.unsubscribe();
      }
      setIsSubscribed(false);
    } finally {
      setIsBusy(false);
    }
  }, [isSupported]);

  return { isSupported, permission, isSubscribed, isBusy, subscribe, unsubscribe };
}
