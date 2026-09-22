// Deliberately NOT "use client" — platform-marketing-page.tsx is a client
// component, and a Server Component (page.tsx) importing a plain data export
// from a "use client" module gets back an RSC client-reference stub instead
// of the real array (works fine as JSX, breaks the moment you call .map on
// it server-side). Living in its own server-safe module lets both the
// client-rendered FAQ accordion and the server-rendered FAQPage JSON-LD
// import the exact same source of truth.
export const FAQS = [
  { q: "Is it really free?", a: "Yes. There's no setup fee, no monthly bill, and no cost to your institution to run your community platform. We handle the details on our side, you focus on your community." },
  { q: "How long does setup take?", a: "Submit the form below and our team will typically reach out within one business day to get your institution's portal configured and ready to launch." },
  { q: "Can we use our own domain or subdomain?", a: "Yes, every institution gets a branded subdomain out of the box, and a custom domain can be configured for your institution as well." },
  { q: "What if our community currently coordinates over WhatsApp or spreadsheets?", a: "That's exactly what this replaces. Import your existing contact list, invite your members, and everything (directory, events, dues, jobs, and updates) moves into one place built for it." },
  { q: "Is our member data secure?", a: "Every institution's data is isolated from every other institution's on the platform, with role-based access control for your admin team." },
  { q: "Can we see it before deciding?", a: "There's no public demo yet, but you don't need one: submit the form below and our team will walk your admin team through a live portal before your institution commits to anything." },
  { q: "Does it work on mobile?", a: "Yes. It's a responsive web app that works in any phone browser, no app store download required, and members can install it to their home screen straight from the browser for an app-like experience." },
];
