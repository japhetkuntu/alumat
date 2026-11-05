self.addEventListener("push", (event) => {
  const data = event.data ? event.data.json() : {};
  const title = data.title || "New notification";
  const isChat = typeof data.actionUrl === "string" && data.actionUrl.includes("/connections/");
  event.waitUntil(
    (async () => {
      // Someone looking at the app already sees a chat message arrive live, so an alert on top of it would only be noise.
      if (isChat) {
        const open = await clients.matchAll({ type: "window", includeUncontrolled: true });
        if (open.some((c) => c.visibilityState === "visible")) return;
      }
      await self.registration.showNotification(title, {
        body: data.body || "",
        icon: "/logo.svg",
        badge: "/logo.svg",
        // One alert per conversation: a newer message replaces the earlier one instead of stacking up.
        ...(isChat ? { tag: data.actionUrl, renotify: true } : {}),
        data: { actionUrl: data.actionUrl || "/notifications" },
      });
    })()
  );
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  const url = event.notification.data?.actionUrl || "/notifications";
  event.waitUntil(
    clients.matchAll({ type: "window", includeUncontrolled: true }).then((windowClients) => {
      for (const client of windowClients) {
        if (client.url.includes(self.location.origin) && "focus" in client) {
          client.navigate(url);
          return client.focus();
        }
      }
      return clients.openWindow(url);
    })
  );
});
