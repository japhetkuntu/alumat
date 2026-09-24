import Script from "next/script";

/** GA4 loader — only ever rendered when NEXT_PUBLIC_GA_MEASUREMENT_ID is set (see
 * layout.tsx), so local dev and any install that hasn't configured a property never
 * sends traffic anywhere. Tracks every host this app answers (the bare marketing
 * domain and every tenant subdomain alike), since both matter for measuring the
 * SEO work done on the shared entity pages and the platform marketing site. */
export function GoogleAnalytics({ measurementId }: { measurementId: string }) {
  return (
    <>
      <Script src={`https://www.googletagmanager.com/gtag/js?id=${measurementId}`} strategy="afterInteractive" />
      <Script id="ga4-init" strategy="afterInteractive">
        {`
          window.dataLayer = window.dataLayer || [];
          function gtag(){dataLayer.push(arguments);}
          gtag('js', new Date());
          gtag('config', '${measurementId}');
        `}
      </Script>
    </>
  );
}
