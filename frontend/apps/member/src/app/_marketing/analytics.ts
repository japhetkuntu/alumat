// Use the site's configured analytics only; never include enquiry contact details.
export function trackMarketing(event: string, detail?: string) {
  const analyticsWindow = window as Window & { gtag?: (...args: unknown[]) => void };
  analyticsWindow.gtag?.("event", event, detail ? { interaction: detail } : {});
}
