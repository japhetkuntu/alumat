"use client";

import { cn, IconTile, CheckCircle2 } from "@alumni/ui";

/**
 * Small, readable pieces of the real product used on the marketing page in place of drawn illustrations.
 * They follow the portal's own look: a flat white panel with a hairline border, square corners, one brand
 * colour, and text the size a member would actually read. The names, amounts and dates are sample data.
 * Every export keeps the old illustration's props so the page can swap them in without changes to the data.
 */
type PanelProps = { className?: string; tone?: "primary" | "accent" };

const muted = { color: "var(--muted-foreground)" } as const;
const strong = { color: "var(--foreground)" } as const;

function Frame({ className, children, toast }: { className?: string; children: React.ReactNode; toast?: string }) {
  return (
    <div aria-hidden="true" className={cn("min-w-0 space-y-3 text-left", className)}>
      <div className="min-w-0 overflow-hidden border p-4" style={{ borderColor: "var(--border)", background: "var(--card)" }}>
        {children}
      </div>
      {toast && (
        <div className="ml-auto flex w-[88%] min-w-0 items-center gap-3 border p-3" style={{ borderColor: "var(--border)", background: "var(--card)" }}>
          <IconTile icon={CheckCircle2} size="sm" tone="primary" filled />
          <p className="min-w-0 flex-1 text-[12px] font-semibold leading-snug" style={strong}>{toast}</p>
        </div>
      )}
    </div>
  );
}

function Avatar({ initials }: { initials: string }) {
  return (
    <span
      className="flex h-8 w-8 shrink-0 items-center justify-center text-[11px] font-bold"
      style={{ background: "var(--muted)", color: "var(--primary)", border: "1px solid var(--border)" }}
    >
      {initials}
    </span>
  );
}

function Tag({ children, solid }: { children: React.ReactNode; solid?: boolean }) {
  return (
    <span
      className="inline-block shrink-0 whitespace-nowrap px-2 py-0.5 text-[10.5px] font-semibold"
      style={solid
        ? { background: "var(--primary)", color: "var(--primary-foreground)" }
        : { border: "1px solid var(--border-emphasis, var(--border))", color: "var(--primary)" }}
    >
      {children}
    </span>
  );
}

function Label({ children }: { children: React.ReactNode }) {
  return <p className="mb-3 text-[10.5px] font-bold uppercase tracking-[0.1em]" style={muted}>{children}</p>;
}

function Line({ children, sub, right }: { children: React.ReactNode; sub?: React.ReactNode; right?: React.ReactNode }) {
  return (
    <div className="flex w-full min-w-0 flex-1 items-center justify-between gap-3">
      <div className="min-w-0 flex-1">
        <p className="text-[13px] font-semibold leading-snug" style={strong}>{children}</p>
        {sub && <p className="text-[11.5px] leading-snug" style={muted}>{sub}</p>}
      </div>
      {right}
    </div>
  );
}

const rule = { borderTop: "1px solid var(--border)" } as const;

/* ── Features ─────────────────────────────────────────────────────────── */

export function DirectoryIllustration({ className }: PanelProps) {
  return (
    <Frame className={className} toast="12 new members joined this week">
      <div className="mb-3 px-3 py-2 text-[12px]" style={{ border: "1px solid var(--border)", ...muted }}>Search by name, chapter or city</div>
      {[
        ["EO", "Esi Owusu", "Accra · joined 2019"],
        ["KB", "Kofi Boateng", "Kumasi · joined 2021"],
        ["AM", "Ama Mensah", "Takoradi · joined 2018"],
      ].map(([i, n, s], k) => (
        <div key={n} className="flex items-center gap-3 py-2.5" style={k ? rule : undefined}>
          <Avatar initials={i} />
          <Line sub={s} right={<Tag>Verified</Tag>}>{n}</Line>
        </div>
      ))}
    </Frame>
  );
}

export function EventsIllustration({ className }: PanelProps) {
  return (
    <Frame className={className} toast="Kofi Boateng just RSVP'd">
      <div className="flex items-center gap-3">
        <div className="flex h-12 w-12 shrink-0 flex-col items-center justify-center" style={{ background: "var(--primary)", color: "var(--primary-foreground)" }}>
          <span className="text-[9.5px] font-bold uppercase tracking-wider">Nov</span>
          <span className="text-[18px] font-bold leading-none">14</span>
        </div>
        <p className="min-w-0 text-[13.5px] font-semibold leading-snug" style={strong}>Annual Members' Dinner</p>
      </div>
      <p className="mt-3 text-[11.5px]" style={muted}>Community Hall · 6:00 pm</p>
      <p className="text-[11.5px]" style={muted}>128 going · 72 spots left</p>
      <div className="mt-4 flex gap-2">
        <span className="flex-1 py-2 text-center text-[12px] font-semibold" style={{ background: "var(--primary)", color: "var(--primary-foreground)" }}>RSVP</span>
        <span className="px-4 py-2 text-[12px] font-semibold" style={{ border: "1px solid var(--border)", ...muted }}>Details</span>
      </div>
    </Frame>
  );
}

export function AlbumsIllustration({ className }: PanelProps) {
  const shades = [30, 16, 24, 12, 20, 34];
  return (
    <Frame className={className} toast="Shared with all 1,204 members">
      <Line sub="48 photos · added by staff" right={<Tag>New</Tag>}>Annual dinner 2025</Line>
      <div className="mt-3 grid grid-cols-3 gap-1.5">
        {shades.map((s, i) => (
          <div key={i} className="aspect-[4/3]" style={{ background: `color-mix(in oklch, var(--primary) ${s}%, var(--muted))` }} />
        ))}
      </div>
    </Frame>
  );
}

export function NotificationsIllustration({ className }: PanelProps) {
  return (
    <Frame className={className} toast="Sent to 1,204 members in seconds">
      {[
        ["New job", "Project Analyst at Northwind", true],
        ["Event reminder", "Annual dinner is this Saturday", true],
        ["Payment received", "GH₵150 for 2026 Annual Dues", false],
      ].map(([t, s, unread], k) => (
        <div key={t as string} className="flex items-start gap-3 py-2.5" style={k ? rule : undefined}>
          <span className="mt-1.5 h-2 w-2 shrink-0" style={{ background: unread ? "var(--primary)" : "var(--border-emphasis, var(--border))" }} />
          <Line sub={s as string}>{t as string}</Line>
        </div>
      ))}
    </Frame>
  );
}

export function JobsIllustration({ className }: PanelProps) {
  return (
    <Frame className={className} toast="3 members applied this week">
      {[
        ["Project Analyst", "Northwind · Kumasi", "Contract"],
        ["Software Engineer", "Acme Tech · Accra", "Full-time"],
      ].map(([t, s, tag], k) => (
        <div key={t} className="py-3" style={k ? rule : { paddingTop: 0 }}>
          <Line sub={s} right={<Tag>{tag}</Tag>}>{t}</Line>
          <p className="mt-1.5 text-[11.5px] font-semibold" style={{ color: "var(--primary)" }}>View details</p>
        </div>
      ))}
    </Frame>
  );
}

export function MentorshipIllustration({ className }: PanelProps) {
  return (
    <Frame className={className} toast="Request accepted, chat opened">
      <div className="flex items-center gap-3">
        <Avatar initials="KA" />
        <Line sub="Product management">Kwame Asante</Line>
      </div>
      <p className="mt-3 text-[11.5px]" style={muted}>2 of 3 mentee spots open</p>
      <span className="mt-3 block py-2 text-center text-[12px] font-semibold" style={{ background: "var(--primary)", color: "var(--primary-foreground)" }}>Request mentorship</span>
    </Frame>
  );
}

export function BusinessIllustration({ className }: PanelProps) {
  return (
    <Frame className={className} toast="New listing added by a member">
      {[
        ["Boateng Farms", "Agribusiness · Kumasi"],
        ["Mensah & Co. Accounting", "Finance · Accra"],
        ["Owusu Print Works", "Printing · Takoradi"],
      ].map(([t, s], k) => (
        <div key={t} className="py-2.5" style={k ? rule : { paddingTop: 0 }}>
          <Line sub={s}>{t}</Line>
        </div>
      ))}
    </Frame>
  );
}

export function SpotlightIllustration({ className }: PanelProps) {
  return (
    <Frame className={className} toast="Shared on the community home page">
      <div className="mb-2 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Avatar initials="AM" />
          <Line sub="Chapter leader, Tamale">Ama Mensah</Line>
        </div>
        <Tag solid>Featured</Tag>
      </div>
      <p className="text-[13px] font-semibold leading-snug" style={strong}>Started a free coding club for girls in Tamale</p>
    </Frame>
  );
}

export function FundraisingIllustration({ className }: PanelProps) {
  return (
    <Frame className={className} toast="GH₵100 received from Kofi B.">
      <Line sub="Help us repair and reopen the hall" right={<Tag>62%</Tag>}>Community Hall Renovation</Line>
      <div className="mt-3 h-2 w-full" style={{ background: "var(--muted)" }}>
        <div className="h-2" style={{ width: "62%", background: "var(--primary)" }} />
      </div>
      <div className="mt-2 flex justify-between text-[11.5px]" style={muted}>
        <span>GH₵12,400 raised of GH₵20,000</span>
        <span>84 contributors</span>
      </div>
      <span className="mt-4 block py-2 text-center text-[12px] font-semibold" style={{ background: "var(--primary)", color: "var(--primary-foreground)" }}>Pay GH₵100</span>
    </Frame>
  );
}

export function StoreIllustration({ className }: PanelProps) {
  return (
    <Frame className={className} toast="Order paid, ready for packing">
      {[
        ["Community tote bag", "GH₵85 · In stock", null],
        ["Order #1042", "Branded cap, size M", "Packed"],
      ].map(([t, s, tag], k) => (
        <div key={t as string} className="py-2.5" style={k ? rule : { paddingTop: 0 }}>
          <Line sub={s as string} right={tag ? <Tag>{tag as string}</Tag> : undefined}>{t as string}</Line>
        </div>
      ))}
    </Frame>
  );
}

export function ServicesIllustration({ className }: PanelProps) {
  return (
    <Frame className={className} toast="Payment received, request opened">
      {[
        ["Membership certificate", "Paid GH₵50 · Ref 2041", "In review"],
        ["Letter of good standing", "Paid GH₵30 · Ref 2038", "Ready"],
      ].map(([t, s, tag], k) => (
        <div key={t} className="py-2.5" style={k ? rule : { paddingTop: 0 }}>
          <Line sub={s} right={<Tag solid={tag === "Ready"}>{tag}</Tag>}>{t}</Line>
        </div>
      ))}
    </Frame>
  );
}

/* ── The three problems ──────────────────────────────────────────────── */

export function ScatteredChatIllustration({ className }: PanelProps) {
  return (
    <Frame className={className}>
      <Label>Where your community lives today</Label>
      {[
        ["WhatsApp: main group", "1,024 members, group is full"],
        ["Facebook group", "No moderator since 2023"],
        ["members-2019-final.xlsx", "Last edited 14 months ago"],
        ["WhatsApp: Kumasi chapter", "Different people, different list"],
      ].map(([t, s], k) => (
        <div key={t} className="py-3" style={k ? rule : { paddingTop: 0 }}>
          <Line sub={s}>{t}</Line>
        </div>
      ))}
    </Frame>
  );
}

export function UnknownAlumniIllustration({ className }: PanelProps) {
  const rows = [
    ["Esi Owusu", "2019", "024 555 0192"],
    ["K. Boateng", "?", "—"],
    ["Ama Mensah", "2018", "—"],
    ["Ama Mensah", "2020", "020 555 0117"],
  ];
  return (
    <Frame className={className}>
      <Label>The member list today</Label>
      <div className="grid grid-cols-[1.4fr_0.6fr_1.2fr] gap-x-3 pb-2 text-[10.5px] font-bold uppercase tracking-wider" style={muted}>
        <span>Name</span><span>Joined</span><span>Phone</span>
      </div>
      {rows.map(([n, y, p], k) => (
        <div key={k} className="grid grid-cols-[1.4fr_0.6fr_1.2fr] gap-x-3 py-2.5 text-[12.5px]" style={rule}>
          <span className="font-semibold" style={strong}>{n}</span>
          <span style={y === "?" ? { color: "var(--destructive)", fontWeight: 600 } : muted}>{y}</span>
          <span style={p === "—" ? { color: "var(--destructive)" } : muted}>{p}</span>
        </div>
      ))}
      <p className="mt-2 text-[11.5px]" style={{ color: "var(--destructive)" }}>Missing details and a possible duplicate</p>
    </Frame>
  );
}

export function ManualReconciliationIllustration({ className }: PanelProps) {
  return (
    <Frame className={className}>
      <Label>Your inbox, two weeks in</Label>
      {[
        ["Kofi: paid! (screenshot.png)", "Not matched to a name"],
        ["Ama: sent 100 by momo", "Amount not checked"],
        ["Esi: done, see attached", "Attachment missing"],
      ].map(([t, s], k) => (
        <div key={t} className="py-3" style={k ? rule : { paddingTop: 0 }}>
          <Line sub={s}>{t}</Line>
        </div>
      ))}
      <div className="mt-2 flex items-baseline justify-between pt-3" style={{ borderTop: "1px solid var(--border-emphasis, var(--border))" }}>
        <span className="text-[11.5px]" style={muted}>Raised so far</span>
        <span className="text-[15px] font-bold" style={strong}>Let me check…</span>
      </div>
    </Frame>
  );
}

/* ── The five limits of a chat group (why-not-whatsapp) ──────────────── */

export function CapIllustration({ className }: PanelProps) {
  return (
    <Frame className={className}>
      <Label>Group info</Label>
      <Line sub="Created by a chapter volunteer">Community Main Group</Line>
      <div className="mt-4 flex items-baseline justify-between">
        <span className="text-[26px] font-bold leading-none" style={strong}>1,024 <span className="text-[13px] font-semibold" style={muted}>of 1,024</span></span>
      </div>
      <div className="mt-2 h-2 w-full" style={{ background: "var(--muted)" }}><div className="h-2 w-full" style={{ background: "var(--destructive)" }} /></div>
      <p className="mt-3 text-[12px] font-semibold" style={{ color: "var(--destructive)" }}>Group is full. New members can't join.</p>
    </Frame>
  );
}

export function NoDirectoryIllustration({ className }: PanelProps) {
  return (
    <Frame className={className}>
      <div className="mb-3 px-3 py-2 text-[12px]" style={{ border: "1px solid var(--border)", ...strong }}>Ama from Takoradi</div>
      <p className="mb-2 text-[11.5px]" style={muted}>3 messages found</p>
      {[
        ["Kofi", "Anyone know Ama's number? 14:02"],
        ["Esi", "Ama from Takoradi joined us last year 09:15"],
        ["Yaw", "Pls add Ama to the list 21:40"],
      ].map(([n, m], k) => (
        <div key={m} className="py-2" style={k ? rule : undefined}>
          <Line sub={m}>{n}</Line>
        </div>
      ))}
      <p className="mt-2 text-[11.5px] font-semibold" style={{ color: "var(--destructive)" }}>Messages, not people. No profile, no filter.</p>
    </Frame>
  );
}

export function OnePhoneIllustration({ className }: PanelProps) {
  return (
    <Frame className={className}>
      <Label>Group admins</Label>
      <div className="flex items-center gap-3">
        <Avatar initials="KA" />
        <Line sub="+233 24 555 0142">Kwame (only admin)</Line>
      </div>
      <div className="mt-4 px-3 py-2.5 text-[12px] font-semibold" style={{ border: "1px solid var(--border)", background: "var(--muted)", ...muted }}>
        This number is no longer in use
      </div>
      <p className="mt-3 text-[11.5px] font-semibold" style={{ color: "var(--destructive)" }}>The group belongs to a phone, not to your organization.</p>
    </Frame>
  );
}

export function NoDataIllustration({ className }: PanelProps) {
  return (
    <Frame className={className}>
      <Label>Ask the group admin</Label>
      {[
        ["Members who paid dues", "Scroll and count"],
        ["Who is coming on Saturday", "Reply with 1 or 2"],
        ["Who is active this year", "No way to tell"],
      ].map(([t, s], k) => (
        <div key={t} className="py-2.5" style={k ? rule : { paddingTop: 0 }}>
          <Line sub={s} right={<span className="text-[15px] font-bold" style={{ color: "var(--destructive)" }}>?</span>}>{t}</Line>
        </div>
      ))}
    </Frame>
  );
}

export function FraudIllustration({ className }: PanelProps) {
  return (
    <Frame className={className}>
      <Label>Group chat</Label>
      <div className="px-3 py-2.5" style={{ border: "1px solid var(--border)", background: "var(--muted)" }}>
        <p className="text-[11.5px] font-semibold" style={muted}>New number · claims to be Kofi</p>
        <p className="mt-1 text-[13px]" style={strong}>Hi all, my usual account is down. Please send GH₵100 for the fundraiser to this number.</p>
      </div>
      <p className="mt-3 text-[11.5px] font-semibold" style={{ color: "var(--destructive)" }}>Members have to spot the fake for themselves.</p>
    </Frame>
  );
}
