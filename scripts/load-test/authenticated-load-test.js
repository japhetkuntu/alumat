// Load test for the AUTHENTICATED member journey — login, then the pages a
// real logged-in member actually uses (dashboard-adjacent reads: directory,
// events, jobs, news, spotlights, own profile). Complements load-test.js,
// which only covers the public marketing/landing pages.
//
// This is the majority of real usage that load-test.js deliberately can't
// cover — once someone's a member, almost everything they do is behind
// login. It needs real test accounts to work, which load-test.js's
// side-effect-free design intentionally avoided providing.
//
// ── REQUIRED SETUP — read before running ───────────────────────────────
// You need a POOL of pre-existing test accounts, not one. Reusing a single
// account across many concurrent virtual users would immediately trip the
// app's own AuthPolicy (10 requests/min per Host+IP — see
// RateLimitingExtensions.cs), which measures nothing useful about capacity.
//
// This script deliberately does NOT create accounts for you — registration
// triggers real OTP emails/SMS, and scripting that against production would
// spam real delivery services and pollute real data. Create a small batch
// (aim for at least as many as your peak VU count, so no two concurrent
// VUs share a session) yourself, once, ahead of time:
//   1. Register N accounts through the real UI/flow, e.g. using an email
//      alias trick (youraddress+loadtest01@gmail.com through +loadtest20@)
//      so they all land in one inbox you can check for OTP codes.
//   2. Complete OTP verification for each (manual, one-time).
//   3. Copy test-accounts.example.json (same folder as this script) to
//      test-accounts.json and fill in the real credentials — that exact
//      filename is already gitignored, so it can never land in a commit.
//   4. Once testing is done, deactivate/delete these accounts — don't leave
//      permanent test data sitting in production member tables.
//
// Each VU logs in ONCE (on its first iteration) and reuses that token for
// the rest of the run — same as a real user's session, and avoids hammering
// the login endpoint on every iteration.
//
// ── Usage ───────────────────────────────────────────────────────────────
// TEST_ACCOUNTS_FILE must point to a JSON file shaped like:
//   [
//     { "email": "youraddress+loadtest01@gmail.com", "password": "..." },
//     { "email": "youraddress+loadtest02@gmail.com", "password": "..." }
//   ]
//
// k6's open() resolves a relative path against THIS SCRIPT'S OWN
// DIRECTORY, not your current shell directory — so if you run k6 from the
// repo root (as below), the value is just the bare filename, not a path
// that already includes scripts/load-test/ again:
//
//   TEST_ACCOUNTS_FILE=test-accounts.json \
//   BASE_URL=https://someslug.alumunion.com \
//     k6 run scripts/load-test/authenticated-load-test.js
//
// An absolute path also works from anywhere and sidesteps this entirely:
//   TEST_ACCOUNTS_FILE="$(pwd)/scripts/load-test/test-accounts.json" \
//     k6 run scripts/load-test/authenticated-load-test.js
//
// Smoke test first, same as load-test.js:
//   LOAD_PROFILE=smoke TEST_ACCOUNTS_FILE=test-accounts.json k6 run scripts/load-test/authenticated-load-test.js
//
// To try a specific peak concurrency without editing this file, use
// PEAK_VUS instead of LOAD_PROFILE — your existing account pool is reused
// automatically, no need to register more accounts to match a higher number:
//   PEAK_VUS=50 TEST_ACCOUNTS_FILE=test-accounts.json k6 run scripts/load-test/authenticated-load-test.js
//
// Keep the peak VU count at or below your account pool size — see
// STAGE_PROFILES below. If VUs exceed the pool, accounts get reused across
// VUs (round-robin), which still runs but no longer means "N distinct
// concurrent sessions."

import http from "k6/http";
import { check, sleep } from "k6";
import { Rate, Trend } from "k6/metrics";

const BASE_URL = __ENV.BASE_URL || "https://alumunion.com";
const ACCOUNTS_FILE = __ENV.TEST_ACCOUNTS_FILE;

if (!ACCOUNTS_FILE) {
  throw new Error(
    "TEST_ACCOUNTS_FILE is required — see this file's header comment for the account-pool setup you need first."
  );
}

// open() only works in k6's init context (top-level, not inside a function),
// so the account pool is loaded once here and shared (read-only) across VUs.
const ACCOUNTS = JSON.parse(open(ACCOUNTS_FILE));
if (!Array.isArray(ACCOUNTS) || ACCOUNTS.length === 0) {
  throw new Error(`${ACCOUNTS_FILE} must be a non-empty JSON array of {email, password}.`);
}

const loginFailures = new Rate("login_failures");
const serverErrors = new Rate("server_errors_5xx");
const timeouts = new Rate("timeouts");
const requestTime = new Trend("authenticated_request_time", true);

const STAGE_PROFILES = {
  smoke: [
    { duration: "20s", target: 3 },
    { duration: "40s", target: 3 },
    { duration: "10s", target: 0 },
  ],
  // Deliberately more conservative than load-test.js's public-page ramp —
  // every VU here costs a real DB-backed authenticated session, not a
  // cached public read, so this is a heavier load per virtual user.
  full: [
    { duration: "1m", target: 10 },
    { duration: "2m", target: 10 },
    { duration: "2m", target: 30 },
    { duration: "3m", target: 30 },
    { duration: "2m", target: 0 },
  ],
};

// PEAK_VUS overrides both named profiles with a simple ramp-up / hold /
// ramp-down to whatever number you pass — no need to hand-edit this file
// each time you want to try a different peak. The account pool doesn't need
// to grow to match: ACCOUNTS[(__VU - 1) % ACCOUNTS.length] below already
// cycles through however many accounts exist, and each VU still performs a
// genuine login and gets its own real session token either way.
//   PEAK_VUS=100 k6 run scripts/load-test/authenticated-load-test.js
function stagesFor(peakVus) {
  if (!peakVus) return STAGE_PROFILES[__ENV.LOAD_PROFILE || "full"];
  const peak = Number(peakVus);
  // Each VU logs in exactly once, on ramp-up — so the ramp duration sets
  // the effective login *rate*, separate from the peak concurrency it's
  // building toward. The app's own AuthPolicy (RateLimitingExtensions.cs)
  // caps logins to 10/min per (Host, IP), all our test traffic sharing one
  // IP — cramming `peak` VUs into a fixed 1-minute ramp means the login
  // rate alone exceeds that ceiling well before reaching any interesting
  // concurrency, and *that's* what was rejecting requests, not nginx. This
  // targets a safe ~6 logins/min (comfortably under 10, leaving headroom
  // for the small queue) by stretching the ramp for larger peaks, so the
  // steady-state hold — the part that actually measures authenticated
  // capacity — is reached without ever tripping the login throttle.
  const rampMinutes = Math.max(1, Math.ceil(peak / 6));
  return [
    { duration: `${rampMinutes}m`, target: peak },
    { duration: "3m", target: peak },
    { duration: "1m", target: 0 },
  ];
}

export const options = {
  stages: stagesFor(__ENV.PEAK_VUS),
  summaryTrendStats: ["avg", "min", "med", "max", "p(90)", "p(95)", "p(99)"],
  thresholds: {
    login_failures: ["rate<0.02"],
    server_errors_5xx: ["rate<0.01"],
    http_req_duration: ["p(95)<3000"],
  },
};

// Module-level (not inside default()) so it persists across this VU's
// iterations — k6 gives each VU its own isolated JS runtime, so this is
// safely per-VU state, not shared/racy across VUs.
let token = null;

function login() {
  const account = ACCOUNTS[(__VU - 1) % ACCOUNTS.length];
  const res = http.post(
    `${BASE_URL}/api/v1/auth/login`,
    JSON.stringify({ Email: account.email, Password: account.password }),
    { headers: { "Content-Type": "application/json" }, tags: { name: "login" } }
  );

  const ok = res.status === 200;
  loginFailures.add(!ok);
  check(res, { "login: succeeded": () => ok });

  if (!ok) {
    console.error(`VU ${__VU}: login failed for ${account.email} (status ${res.status}): ${res.body}`);
    return null;
  }

  try {
    return res.json("data.tokens.accessToken");
  } catch (e) {
    console.error(`VU ${__VU}: couldn't parse login response: ${res.body}`);
    return null;
  }
}

function authGet(path, name) {
  const res = http.get(`${BASE_URL}${path}`, {
    headers: { Authorization: `Bearer ${token}` },
    tags: { name },
  });
  requestTime.add(res.timings.duration);
  serverErrors.add(res.status >= 500);
  timeouts.add(res.status === 0);
  check(res, {
    [`${name}: got a response`]: (r) => r.status !== 0,
    [`${name}: not a server error`]: (r) => r.status < 500,
    [`${name}: not unauthorized`]: (r) => r.status !== 401, // a 401 here means the token/session broke mid-run
  });
}

// Weighted like real usage: profile check and directory browsing are common,
// less-visited sections (mentorship, forum) are rarer.
const AUTHENTICATED_JOURNEYS = [
  { weight: 3, path: "/api/v1/auth/me", name: "profile" },
  { weight: 4, path: "/api/v1/directory", name: "directory" },
  { weight: 3, path: "/api/v1/events", name: "events" },
  { weight: 2, path: "/api/v1/jobs", name: "jobs" },
  { weight: 2, path: "/api/v1/news", name: "news" },
  { weight: 1, path: "/api/v1/spotlights", name: "spotlights" },
];

function pickJourney() {
  const total = AUTHENTICATED_JOURNEYS.reduce((sum, j) => sum + j.weight, 0);
  let r = Math.random() * total;
  for (const j of AUTHENTICATED_JOURNEYS) {
    if (r < j.weight) return j;
    r -= j.weight;
  }
  return AUTHENTICATED_JOURNEYS[0];
}

export default function () {
  if (!token) {
    token = login();
    if (!token) {
      // Back off hard rather than retry-looping a broken login — that
      // would itself become an unintended brute-force pattern against the
      // account, tripping the very auth-abuse protection we're trying not
      // to trigger.
      sleep(10);
      return;
    }
  }

  const journey = pickJourney();
  authGet(journey.path, journey.name);
  sleep(Math.random() * 3 + 1);
}

export function handleSummary(data) {
  const pct = (rate) => `${(rate * 100).toFixed(2)}%`;
  const m = data.metrics;
  const summary = `
================================================================================
AUTHENTICATED LOAD TEST SUMMARY — ${BASE_URL}
================================================================================
Total requests:          ${m.http_reqs?.values?.count ?? "n/a"}
Peak virtual users:       ${m.vus_max?.values?.value ?? "n/a"}
Account pool size:        ${ACCOUNTS.length}

Login failures:           ${m.login_failures?.values?.rate != null ? pct(m.login_failures.values.rate) : "n/a"}
  -> Should be ~0%. If not, check whether the account pool is stale
     (passwords changed, accounts deactivated) before assuming it's a
     capacity problem.

Server errors (5xx):      ${m.server_errors_5xx?.values?.rate != null ? pct(m.server_errors_5xx.values.rate) : "n/a"}
  -> The number that actually matters for capacity.

Timeouts/conn failures:   ${m.timeouts?.values?.rate != null ? pct(m.timeouts.values.rate) : "n/a"}

p95 response time:        ${m.http_req_duration?.values?.["p(95)"]?.toFixed(0) ?? "n/a"} ms
p99 response time:        ${m.http_req_duration?.values?.["p(99)"]?.toFixed(0) ?? "n/a"} ms
================================================================================
`;
  return {
    stdout: summary,
    "authenticated-load-test-results.json": JSON.stringify(data, null, 2),
  };
}
