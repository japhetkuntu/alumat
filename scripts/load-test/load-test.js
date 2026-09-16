// Load test for the AlumUnion marketing site + public API — run this from
// your OWN machine (laptop, a separate box), never from the droplet itself:
// running the load generator on the same server it's hitting just steals the
// server's own CPU/network and gives you meaningless numbers.
//
// SAFETY — this deliberately only hits read-only, public, unauthenticated
// endpoints. No login/register/payment paths: those have side effects (real
// accounts created, real OTP emails/SMS sent, real Paystack calls) that a
// load test must not trigger. This measures raw capacity, not business logic.
//
// You will hit nginx's rate limits (api_general: 50r/s, burst 100, per IP —
// see deploy/nginx.ssl.conf) before you hit the app's real ceiling, since a
// load generator on one machine is one IP. That's expected and useful to see
// (confirms the limiter works under real load), not a sign the app is broken
// — watch for 429s specifically vs. 500s/502s/timeouts, which are different
// findings (see the summary k6 prints at the end).
//
// Install: https://k6.io/docs/get-started/installation/
//   macOS:  brew install k6
//   Linux:  see k6 docs (apt repo, or download a static binary)
//
// Run against the marketing/member domain (default, no slug — landing page +
// public reads that don't depend on a specific institution existing):
//   k6 run scripts/load-test/load-test.js
//
// Run against a specific institution's subdomain instead:
//   BASE_URL=https://someslug.alumunion.com k6 run scripts/load-test/load-test.js
//
// Override the ramp shape (see stages below) for a quicker smoke test:
//   LOAD_PROFILE=smoke k6 run scripts/load-test/load-test.js
//
// (Not named K6_STAGES: that's a reserved k6 env var that maps directly to
// the native `stages` option as JSON — reusing it made k6 try to parse
// "smoke" as its own stages config and fail before this script even ran.)

import http from "k6/http";
import { check, sleep } from "k6";
import { Rate, Trend } from "k6/metrics";

const BASE_URL = __ENV.BASE_URL || "https://alumunion.com";

// Separate custom metrics so the end-of-run summary clearly distinguishes
// "the rate limiter did its job" (expected, fine) from "the server actually
// failed" (the thing you're trying to find out).
const rateLimited = new Rate("rate_limited_429");
const serverErrors = new Rate("server_errors_5xx");
const timeouts = new Rate("timeouts");
const pageLoadTime = new Trend("page_load_time", true);

// Two ramp profiles. "smoke" is a 2-minute sanity check that everything's
// wired correctly before committing to the full 12-minute run. Default is
// the real test: gradual ramp to 200 concurrent virtual users (a genuinely
// heavy peak for an alumni portal — see the conversation this script came
// from for why 200 concurrent is already a stretch target relative to what
// "10,000 registered members" actually implies in practice), held, then
// ramped down. Adjust the peak VU count directly below if you want to probe
// higher or lower.
const STAGE_PROFILES = {
  smoke: [
    { duration: "20s", target: 5 },
    { duration: "40s", target: 5 },
    { duration: "10s", target: 0 },
  ],
  full: [
    { duration: "1m", target: 200 },   // warm-up
    { duration: "2m", target: 200 },   // hold — baseline
    { duration: "1m", target: 750 },   // ramp
    { duration: "2m", target: 750 },   // hold — moderate peak
    { duration: "1m", target: 2000 },  // ramp — heavy peak
    { duration: "3m", target: 2000 },  // hold — sustained heavy peak
    { duration: "2m", target: 0 },    // ramp down
  ],
};

export const options = {
  stages: STAGE_PROFILES[__ENV.LOAD_PROFILE || "full"],
  // p(99) isn't computed by default — handleSummary below reads it, so it
  // has to be requested explicitly or that lookup comes back undefined.
  summaryTrendStats: ["avg", "min", "med", "max", "p(90)", "p(95)", "p(99)"],
  thresholds: {
    // Deliberately NOT using k6's built-in http_req_failed here: it
    // auto-flags any non-2xx/3xx response as "failed" by default, but the
    // institution-scoped endpoints (theme, news) correctly 404 when hit
    // against the bare marketing domain with no institution resolved —
    // that's expected behavior, not a failure. server_errors_5xx (our own
    // metric, only counts real 5xx) is the meaningful signal instead.
    server_errors_5xx: ["rate<0.01"],     // <1% actual server errors
    http_req_duration: ["p(95)<3000"],    // 95% of requests under 3s
  },
};

// Public, side-effect-free endpoints only. Weighted so the mix roughly
// resembles real traffic: most visitors just look at pages, a smaller
// fraction of requests are the API calls those pages themselves make.
const JOURNEYS = [
  { weight: 5, run: visitHomepage },
  { weight: 3, run: fetchPublicTheme },
  { weight: 2, run: fetchPublicNews },
  { weight: 1, run: visitWhyNotWhatsapp },
];

function pickJourney() {
  const total = JOURNEYS.reduce((sum, j) => sum + j.weight, 0);
  let r = Math.random() * total;
  for (const j of JOURNEYS) {
    if (r < j.weight) return j.run;
    r -= j.weight;
  }
  return JOURNEYS[0].run;
}

function record(res) {
  pageLoadTime.add(res.timings.duration);
  rateLimited.add(res.status === 429);
  serverErrors.add(res.status >= 500);
  timeouts.add(res.status === 0); // k6 reports status 0 on connection/timeout failure
}

function visitHomepage() {
  const res = http.get(`${BASE_URL}/`, { tags: { name: "homepage" } });
  record(res);
  check(res, {
    "homepage: got a response": (r) => r.status !== 0,
    "homepage: not a server error": (r) => r.status < 500,
  });
}

function fetchPublicTheme() {
  const res = http.get(`${BASE_URL}/api/v1/public/institution/theme`, {
    tags: { name: "public_theme" },
  });
  record(res);
  check(res, {
    "theme: got a response": (r) => r.status !== 0,
    "theme: not a server error": (r) => r.status < 500,
  });
}

function fetchPublicNews() {
  const res = http.get(`${BASE_URL}/api/v1/public/news`, { tags: { name: "public_news" } });
  record(res);
  check(res, {
    "news: got a response": (r) => r.status !== 0,
    "news: not a server error": (r) => r.status < 500,
  });
}

function visitWhyNotWhatsapp() {
  const res = http.get(`${BASE_URL}/why-not-whatsapp`, { tags: { name: "why_not_whatsapp" } });
  record(res);
  check(res, {
    "why-not-whatsapp: got a response": (r) => r.status !== 0,
    "why-not-whatsapp: not a server error": (r) => r.status < 500,
  });
}

export default function () {
  pickJourney()();
  // A real visitor doesn't fire requests back-to-back — this spacing keeps
  // the VU count meaningful (200 VUs ≈ roughly 200 people idly browsing,
  // not 200 threads hammering as fast as possible, which would test
  // something closer to a DoS than realistic peak traffic).
  sleep(Math.random() * 3 + 1);
}

export function handleSummary(data) {
  const pct = (rate) => `${(rate * 100).toFixed(2)}%`;
  const m = data.metrics;
  const summary = `
================================================================================
LOAD TEST SUMMARY — ${BASE_URL}
================================================================================
Total requests:        ${m.http_reqs?.values?.count ?? "n/a"}
Peak virtual users:     ${m.vus_max?.values?.value ?? "n/a"}

Rate-limited (429):     ${m.rate_limited_429?.values?.rate != null ? pct(m.rate_limited_429.values.rate) : "n/a"}
  -> Expected under heavy load; this is nginx's api_general zone doing its
     job (50 req/s + burst 100 per source IP). A high number here just means
     you found the limiter's ceiling, not the app's — rerun from multiple
     source IPs (or raise the zone's rate/burst temporarily) if you want to
     push past it and find the *app's* real ceiling.

Server errors (5xx):    ${m.server_errors_5xx?.values?.rate != null ? pct(m.server_errors_5xx.values.rate) : "n/a"}
  -> This is the number that actually matters. Anything meaningfully above
     0% means the app itself is failing under this load, not just being
     told to slow down.

Timeouts/conn failures: ${m.timeouts?.values?.rate != null ? pct(m.timeouts.values.rate) : "n/a"}
  -> Connection refused, DNS failure, or request never completed. Also a
     real finding, not a rate-limit artifact.

p95 response time:      ${m.http_req_duration?.values?.["p(95)"]?.toFixed(0) ?? "n/a"} ms
p99 response time:      ${m.http_req_duration?.values?.["p(99)"]?.toFixed(0) ?? "n/a"} ms
================================================================================
`;
  return {
    stdout: summary,
    "load-test-results.json": JSON.stringify(data, null, 2),
  };
}
