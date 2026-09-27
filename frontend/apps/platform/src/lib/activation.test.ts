import { describe, expect, it } from "vitest";
import { activationStatus, scorecardToCsv, trialLabel } from "./activation";
import { parseCsv } from "./csv";
import type { ActivationScorecardItem } from "./platform-api";

const now = new Date("2026-10-07T12:00:00Z");

function item(overrides: Partial<ActivationScorecardItem> = {}): ActivationScorecardItem {
  return {
    institutionId: "i1",
    name: "Test OSA",
    slug: "test",
    onboardedAt: "2026-09-20T00:00:00Z",
    activatedAt: null,
    daysLive: 17,
    criteria: [
      { key: "branding", label: "Branded portal", met: true, detail: "Logo, hero photo and stories set" },
      { key: "payouts", label: "Payouts live", met: false, detail: "No bank details submitted" },
      { key: "members", label: "Members on board", met: false, detail: "40/100 members · 20% signed in" },
      { key: "payments", label: "Campaign and online payment", met: false, detail: "0 active campaigns · 0 online payments" },
      { key: "staff", label: "Staff active weekly", met: false, detail: "1 staff this week · 0/3 week streak" },
    ],
    metCount: 1,
    nextStep: "Submit your settlement bank details.",
    isStalled: true,
    isActivated: false,
    isOverdue: false,
    minMembers: 100,
    trialEndsAt: null,
    setupNudgesEnabled: true,
    ...overrides,
  };
}

describe("trialLabel", () => {
  it("counts down to the trial end", () => {
    expect(trialLabel("2026-10-10T12:00:00Z", false, now)).toBe("Trial ends in 3 days");
    expect(trialLabel("2026-10-08T12:00:00Z", false, now)).toBe("Trial ends in 1 day");
  });

  it("shows a recently ended trial but hides old default trial dates", () => {
    expect(trialLabel("2026-10-04T12:00:00Z", false, now)).toBe("Trial ended 3 days ago");
    expect(trialLabel("2026-09-01T12:00:00Z", false, now)).toBeNull();
  });

  it("says nothing once activated or without a date", () => {
    expect(trialLabel("2026-10-10T12:00:00Z", true, now)).toBeNull();
    expect(trialLabel(null, false, now)).toBeNull();
  });
});

describe("activationStatus", () => {
  it("prefers activated, then overdue, then stalled", () => {
    expect(activationStatus(item({ isActivated: true, isOverdue: true }))).toBe("Activated");
    expect(activationStatus(item({ isOverdue: true }))).toBe("Overdue");
    expect(activationStatus(item())).toBe("Stalled");
    expect(activationStatus(item({ isStalled: false }))).toBe("In progress");
  });
});

describe("scorecardToCsv", () => {
  it("writes one row per institution with a yes/no and detail per criterion", () => {
    const [header, row] = parseCsv(scorecardToCsv([item()]));
    expect(header.slice(0, 6)).toEqual(["Institution", "Status", "Days live", "Criteria met", "Branding", "Branding detail"]);
    expect(row.slice(0, 8)).toEqual([
      "Test OSA", "Stalled", "17", "1/5", "Yes", "Logo, hero photo and stories set", "No", "No bank details submitted",
    ]);
    expect(row.at(-2)).toBe("Submit your settlement bank details.");
    expect(row.at(-1)).toBe("");
  });
});
