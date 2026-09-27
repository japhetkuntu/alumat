import { describe, expect, it } from "vitest";
import { parseCsv, toCsv } from "./csv";
import { followUpState, parseLeadCsv } from "./leads";

describe("parseCsv", () => {
  it("handles quoted commas, escaped quotes, CRLF and a BOM", () => {
    const text = '﻿Name,Note\r\n"Achimota, OSA","She said ""yes"""\r\nPRESEC,\r\n';
    expect(parseCsv(text)).toEqual([
      ["Name", "Note"],
      ["Achimota, OSA", 'She said "yes"'],
      ["PRESEC", ""],
    ]);
  });

  it("keeps newlines inside quoted fields and drops blank lines", () => {
    expect(parseCsv('a,b\n"line one\nline two",x\n\n')).toEqual([
      ["a", "b"],
      ["line one\nline two", "x"],
    ]);
  });

  it("round-trips through toCsv", () => {
    const rows = [["a,b", 'q"uote'], ["plain", ""]];
    expect(parseCsv(toCsv(rows))).toEqual(rows);
  });
});

describe("parseLeadCsv", () => {
  it("maps header aliases, stage names and follow-up dates", () => {
    const csv = [
      "School,Contact Person,Phone Number,Stage,Follow up,Unknown column",
      "Achimota OSA,Ama Mensah,0240000000,demo booked,2026-10-06,x",
      "PRESEC OSA,Kofi Boateng,,Trial,,",
    ].join("\n");

    const result = parseLeadCsv(csv, "Outreach");

    expect(result.error).toBeUndefined();
    expect(result.ignoredHeaders).toEqual(["Unknown column"]);
    expect(result.rows).toEqual([
      {
        institutionName: "Achimota OSA",
        contactName: "Ama Mensah",
        contactPhone: "0240000000",
        status: "DemoBooked",
        nextFollowUpAt: "2026-10-06",
        source: "Outreach",
      },
      { institutionName: "PRESEC OSA", contactName: "Kofi Boateng", status: "Trial", source: "Outreach" },
    ]);
  });

  it("keeps a row's own source over the default", () => {
    const result = parseLeadCsv("Institution,Contact,Source\nA,B,Referral", "Outreach");
    expect(result.rows[0].source).toBe("Referral");
  });

  it("passes unknown stages through for the server to report", () => {
    const result = parseLeadCsv("Institution,Contact,Stage\nA,B,Maybe later", "Outreach");
    expect(result.rows[0].status).toBe("Maybe later");
  });

  it("rejects a file without an institution column", () => {
    const result = parseLeadCsv("Contact,Phone\nAma,024", "Outreach");
    expect(result.error).toMatch(/No institution column/);
    expect(result.rows).toEqual([]);
  });

  it("reports an empty file", () => {
    expect(parseLeadCsv("", "Outreach").error).toBe("The file is empty.");
  });
});

describe("followUpState", () => {
  const now = new Date(2026, 9, 7, 15, 0); // 7 Oct 2026, local time

  it("classifies relative to today's calendar date", () => {
    expect(followUpState("2026-10-06T00:00:00Z", now)).toBe("overdue");
    expect(followUpState("2026-10-07T00:00:00Z", now)).toBe("today");
    expect(followUpState("2026-10-08T00:00:00Z", now)).toBe("upcoming");
  });

  it("returns null without a date", () => {
    expect(followUpState(null, now)).toBeNull();
    expect(followUpState(undefined, now)).toBeNull();
  });
});
