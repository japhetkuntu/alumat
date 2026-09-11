"use client";

import { cn } from "@alumni/ui";

/**
 * Shared "organic blob" frame for a feature illustration — an irregular,
 * asymmetric border-radius (the standard CSS blob technique) instead of a
 * plain rectangle, so an illustration reads as a deliberate graphic element
 * rather than another bordered card. Each illustration composes simple flat
 * shapes on top of this rather than literal artwork — legible at a glance,
 * and themed entirely from the platform's own primary/accent tokens rather
 * than a fixed illustration palette.
 */
function Blob({ tone, className, children }: { tone: "primary" | "accent"; className?: string; children: React.ReactNode }) {
  const bg = tone === "accent"
    ? "linear-gradient(135deg, var(--brand-accent-light, var(--brand-accent, var(--muted))) 0%, color-mix(in oklch, var(--brand-accent, var(--primary)) 10%, var(--background)) 100%)"
    : "linear-gradient(135deg, var(--brand-primary-100, var(--muted)) 0%, color-mix(in oklch, var(--primary) 10%, var(--background)) 100%)";
  return (
    <div
      className={cn("relative flex items-center justify-center overflow-hidden shrink-0", className)}
      style={{ background: bg, borderRadius: "42% 58% 65% 35% / 45% 40% 60% 55%" }}
      aria-hidden="true"
    >
      {children}
    </div>
  );
}

/** Careers/jobs feature — a bulletin board with a few postings pinned on it, a briefcase badge overlapping the corner. */
export function JobsIllustration({ className, tone = "primary" }: { className?: string; tone?: "primary" | "accent" }) {
  const c = tone === "accent" ? "var(--brand-accent, var(--primary))" : "var(--primary)";
  return (
    <Blob tone={tone} className={className}>
      <svg viewBox="0 0 140 120" className="w-[68%] h-[68%]">
        <rect x="14" y="14" width="112" height="92" rx="10" fill="var(--card)" stroke="var(--border)" />
        <g transform="rotate(-6 44 46)">
          <rect x="24" y="30" width="40" height="30" rx="4" fill="var(--brand-primary-100, var(--muted))" stroke={c} strokeOpacity="0.35" />
          <rect x="30" y="38" width="20" height="3" rx="1.5" fill={c} opacity="0.6" />
          <rect x="30" y="45" width="26" height="3" rx="1.5" fill="var(--muted-foreground)" opacity="0.4" />
        </g>
        <g transform="rotate(4 96 52)">
          <rect x="76" y="36" width="40" height="30" rx="4" fill="var(--brand-accent-light, var(--card))" stroke="var(--brand-accent, var(--primary))" strokeOpacity="0.35" />
          <rect x="82" y="44" width="20" height="3" rx="1.5" fill="var(--brand-accent, var(--primary))" opacity="0.7" />
          <rect x="82" y="51" width="26" height="3" rx="1.5" fill="var(--muted-foreground)" opacity="0.4" />
        </g>
        <g transform="rotate(-3 60 88)">
          <rect x="40" y="74" width="44" height="26" rx="4" fill="var(--card)" stroke="var(--border)" />
          <rect x="46" y="81" width="22" height="3" rx="1.5" fill={c} opacity="0.5" />
          <rect x="46" y="88" width="30" height="3" rx="1.5" fill="var(--muted-foreground)" opacity="0.35" />
        </g>
      </svg>
      <div className="absolute -bottom-2 -right-2 w-11 h-11 rounded-2xl flex items-center justify-center border"
        style={{ background: c, borderColor: "var(--card)" }}>
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <rect x="2" y="7" width="20" height="14" rx="2" /><path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16" />
        </svg>
      </div>
    </Blob>
  );
}

/** Mentorship feature — two avatars linked by a guiding dashed path with a spark at the midpoint. */
export function MentorshipIllustration({ className, tone = "accent" }: { className?: string; tone?: "primary" | "accent" }) {
  const c = tone === "accent" ? "var(--brand-accent, var(--primary))" : "var(--primary)";
  return (
    <Blob tone={tone} className={className}>
      <svg viewBox="0 0 140 120" className="w-[70%] h-[70%]">
        <path d="M34 78 C 55 40, 85 40, 106 78" fill="none" stroke="var(--muted-foreground)" strokeOpacity="0.35" strokeWidth="2.5" strokeDasharray="1 8" strokeLinecap="round" />
        <circle cx="70" cy="34" r="9" fill={c} opacity="0.9" />
        <path d="M67 34 l2 2.5 l4.5 -5.5" stroke="white" strokeWidth="1.6" fill="none" strokeLinecap="round" strokeLinejoin="round" />
        <g>
          <circle cx="34" cy="82" r="20" fill="var(--card)" stroke="var(--border)" />
          <circle cx="34" cy="76" r="6.5" fill="var(--primary)" opacity="0.85" />
          <path d="M22 92 a12 10 0 0 1 24 0" fill="var(--primary)" opacity="0.85" />
        </g>
        <g>
          <circle cx="106" cy="82" r="20" fill="var(--card)" stroke="var(--border)" />
          <circle cx="106" cy="76" r="6.5" fill={c} opacity="0.85" />
          <path d="M94 92 a12 10 0 0 1 24 0" fill={c} opacity="0.85" />
        </g>
      </svg>
    </Blob>
  );
}

/** Directory feature — a member list card: a search bar and a few rows, each a small avatar dot plus name/detail bars. */
export function DirectoryIllustration({ className, tone = "primary" }: { className?: string; tone?: "primary" | "accent" }) {
  const c = tone === "accent" ? "var(--brand-accent, var(--primary))" : "var(--primary)";
  return (
    <Blob tone={tone} className={className}>
      <svg viewBox="0 0 140 120" className="w-[68%] h-[68%]">
        <rect x="16" y="14" width="108" height="92" rx="10" fill="var(--card)" stroke="var(--border)" />
        <rect x="26" y="24" width="88" height="14" rx="7" fill="var(--muted)" />
        <circle cx="35" cy="31" r="3.4" fill="none" stroke={c} strokeWidth="1.6" />
        <line x1="39" y1="34" x2="44" y2="39" stroke={c} strokeWidth="1.6" strokeLinecap="round" />
        {[54, 74, 94].map((y, i) => (
          <g key={y}>
            <circle cx="34" cy={y} r="7" fill={i === 1 ? c : "var(--muted)"} opacity={i === 1 ? 0.85 : 1} />
            <rect x="48" y={y - 5} width={i === 1 ? 44 : 36} height="3.2" rx="1.6" fill="var(--muted-foreground)" opacity="0.55" />
            <rect x="48" y={y + 1} width={i === 1 ? 30 : 24} height="3.2" rx="1.6" fill="var(--muted-foreground)" opacity="0.3" />
          </g>
        ))}
      </svg>
    </Blob>
  );
}

/** Fundraising feature — coins stacking up inside a jar, a small rising "+" above it. */
export function FundraisingIllustration({ className, tone = "primary" }: { className?: string; tone?: "primary" | "accent" }) {
  const c = tone === "accent" ? "var(--brand-accent, var(--primary))" : "var(--primary)";
  return (
    <Blob tone={tone} className={className}>
      <svg viewBox="0 0 140 120" className="w-[64%] h-[64%]">
        <path d="M42 40 h56 l-8 58 a8 8 0 0 1 -8 7 h-24 a8 8 0 0 1 -8 -7 z" fill="var(--card)" stroke="var(--border)" strokeWidth="2" />
        <ellipse cx="70" cy="40" rx="28" ry="7" fill="var(--card)" stroke="var(--border)" strokeWidth="2" />
        {[86, 74, 62].map((y, i) => (
          <ellipse key={y} cx="70" cy={y} rx={22 - i * 2} ry="6" fill={c} opacity={0.35 + i * 0.22} />
        ))}
        <circle cx="98" cy="24" r="11" fill={c} />
        <path d="M98 19v10M93 24h10" stroke="white" strokeWidth="2" strokeLinecap="round" />
      </svg>
    </Blob>
  );
}

/** Events feature — a calendar card with one highlighted date and a location pin peeking from behind. */
export function EventsIllustration({ className, tone = "primary" }: { className?: string; tone?: "primary" | "accent" }) {
  const c = tone === "accent" ? "var(--brand-accent, var(--primary))" : "var(--primary)";
  return (
    <Blob tone={tone} className={className}>
      <svg viewBox="0 0 140 120" className="w-[66%] h-[66%]">
        <path d="M96 30 a18 18 0 1 1 -36 0 a18 18 0 0 1 36 0 Z" fill="none" />
        <path d="M78 96 C 78 96 100 76 100 58 a22 22 0 1 0 -44 0 c0 18 22 38 22 38 Z" fill={c} opacity="0.16" />
        <circle cx="78" cy="58" r="9" fill={c} opacity="0.5" />
        <rect x="18" y="24" width="80" height="72" rx="9" fill="var(--card)" stroke="var(--border)" strokeWidth="2" />
        <rect x="18" y="24" width="80" height="18" rx="9" fill={c} opacity="0.85" />
        <rect x="30" y="16" width="4" height="14" rx="2" fill={c} />
        <rect x="82" y="16" width="4" height="14" rx="2" fill={c} />
        {[0, 1, 2].map((row) =>
          [0, 1, 2, 3].map((col) => {
            const isMarked = row === 1 && col === 2;
            return (
              <rect key={`${row}-${col}`} x={28 + col * 16} y={52 + row * 14} width="10" height="10" rx="3"
                fill={isMarked ? c : "var(--muted)"} opacity={isMarked ? 1 : 0.7} />
            );
          })
        )}
      </svg>
    </Blob>
  );
}

/** Store feature — a shopping bag with a price tag badge overlapping the corner. */
export function StoreIllustration({ className, tone = "primary" }: { className?: string; tone?: "primary" | "accent" }) {
  const c = tone === "accent" ? "var(--brand-accent, var(--primary))" : "var(--primary)";
  return (
    <Blob tone={tone} className={className}>
      <svg viewBox="0 0 140 120" className="w-[62%] h-[62%]">
        <path d="M40 46 h60 l6 52 a8 8 0 0 1 -8 9 H42 a8 8 0 0 1 -8 -9 Z" fill="var(--card)" stroke="var(--border)" strokeWidth="2" />
        <path d="M52 46 v-8 a18 18 0 0 1 36 0 v8" fill="none" stroke={c} strokeWidth="4" strokeLinecap="round" />
        <rect x="48" y="62" width="44" height="3.4" rx="1.7" fill="var(--muted-foreground)" opacity="0.35" />
        <rect x="48" y="72" width="30" height="3.4" rx="1.7" fill="var(--muted-foreground)" opacity="0.25" />
      </svg>
      <div className="absolute -top-1 -right-1 w-11 h-11 rounded-2xl flex items-center justify-center border rotate-6"
        style={{ background: c, borderColor: "var(--card)" }}>
        <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M20.59 13.41 11 3.83 3.83 11l9.58 9.59a2 2 0 0 0 2.83 0l4.35-4.35a2 2 0 0 0 0-2.83Z" />
          <circle cx="7.5" cy="7.5" r="1" fill="white" stroke="none" />
        </svg>
      </div>
    </Blob>
  );
}

/** Photo Albums feature — a fanned stack of photo cards, each with a tiny landscape glyph. */
export function AlbumsIllustration({ className, tone = "primary" }: { className?: string; tone?: "primary" | "accent" }) {
  const c = tone === "accent" ? "var(--brand-accent, var(--primary))" : "var(--primary)";
  return (
    <Blob tone={tone} className={className}>
      <svg viewBox="0 0 140 120" className="w-[68%] h-[68%]">
        {[
          { x: 32, y: 26, r: -10 },
          { x: 56, y: 20, r: 8 },
          { x: 44, y: 30, r: -1 },
        ].map((p, i) => (
          <g key={i} transform={`rotate(${p.r} ${p.x + 26} ${p.y + 22})`}>
            <rect x={p.x} y={p.y} width="52" height="44" rx="5" fill="var(--card)" stroke="var(--border)" strokeWidth="1.6" />
            <rect x={p.x + 5} y={p.y + 5} width="42" height="26" rx="2" fill={i === 2 ? c : "var(--muted)"} opacity={i === 2 ? 0.3 : 1} />
            {i === 2 && (
              <>
                <circle cx={p.x + 15} cy={p.y + 14} r="4" fill={c} opacity="0.7" />
                <path d={`M${p.x + 8} ${p.y + 30} l10 -10 l8 8 l8 -12 l8 14 z`} fill={c} opacity="0.5" />
              </>
            )}
            <rect x={p.x + 5} y={p.y + 34} width="26" height="3" rx="1.5" fill="var(--muted-foreground)" opacity="0.3" />
          </g>
        ))}
      </svg>
    </Blob>
  );
}

/** Spotlight feature — a five-point star inside a soft radiating beam, a couple of sparkle accents. */
export function SpotlightIllustration({ className, tone = "primary" }: { className?: string; tone?: "primary" | "accent" }) {
  const c = tone === "accent" ? "var(--brand-accent, var(--primary))" : "var(--primary)";
  return (
    <Blob tone={tone} className={className}>
      <svg viewBox="0 0 140 120" className="w-[62%] h-[62%]">
        <path d="M70 10 L40 100 L70 108 L100 100 Z" fill={c} opacity="0.12" />
        <path d="M70 24 L48 96 L70 102 L92 96 Z" fill={c} opacity="0.14" />
        <path d="M70 46 l7.5 15.5 17 2.5 -12.3 12 2.9 17 -15.1 -8 -15.1 8 2.9 -17 -12.3 -12 17 -2.5 Z" fill={c} />
        <circle cx="102" cy="34" r="3" fill={c} opacity="0.6" />
        <circle cx="34" cy="50" r="2" fill={c} opacity="0.5" />
      </svg>
    </Blob>
  );
}

/** Business directory feature — a small storefront building with a location pin beside it. */
export function BusinessIllustration({ className, tone = "primary" }: { className?: string; tone?: "primary" | "accent" }) {
  const c = tone === "accent" ? "var(--brand-accent, var(--primary))" : "var(--primary)";
  return (
    <Blob tone={tone} className={className}>
      <svg viewBox="0 0 140 120" className="w-[66%] h-[66%]">
        <rect x="26" y="40" width="60" height="60" rx="6" fill="var(--card)" stroke="var(--border)" strokeWidth="2" />
        <rect x="26" y="40" width="60" height="12" rx="6" fill={c} opacity="0.8" />
        {[0, 1].map((row) => [0, 1, 2].map((col) => (
          <rect key={`${row}-${col}`} x={34 + col * 16} y={60 + row * 18} width="10" height="10" rx="2" fill="var(--muted)" />
        )))}
        <rect x="50" y="82" width="12" height="18" rx="1.5" fill={c} opacity="0.6" />
        <path d="M104 44 c0 16 -14 22 -14 38 c0 -16 -14 -22 -14 -38 a14 14 0 0 1 28 0 Z" fill={c} opacity="0.9" />
        <circle cx="90" cy="44" r="5.5" fill="var(--card)" />
      </svg>
    </Blob>
  );
}

/** Services feature — an official document (transcript/letter) with a letterhead stripe and text lines, an "approved" stamp overlapping the corner. */
export function ServicesIllustration({ className, tone = "primary" }: { className?: string; tone?: "primary" | "accent" }) {
  const c = tone === "accent" ? "var(--brand-accent, var(--primary))" : "var(--primary)";
  return (
    <Blob tone={tone} className={className}>
      <svg viewBox="0 0 140 120" className="w-[64%] h-[64%]">
        <g transform="rotate(-4 62 58)">
          <rect x="26" y="16" width="72" height="92" rx="8" fill="var(--card)" stroke="var(--border)" strokeWidth="2" />
          <rect x="26" y="16" width="72" height="14" rx="8" fill={c} opacity="0.85" />
          <rect x="38" y="42" width="48" height="4" rx="2" fill="var(--muted-foreground)" opacity="0.35" />
          <rect x="38" y="52" width="40" height="4" rx="2" fill="var(--muted-foreground)" opacity="0.35" />
          <rect x="38" y="62" width="44" height="4" rx="2" fill="var(--muted-foreground)" opacity="0.35" />
          <rect x="38" y="72" width="30" height="4" rx="2" fill="var(--muted-foreground)" opacity="0.35" />
          <rect x="38" y="86" width="26" height="8" rx="4" fill="var(--muted)" />
        </g>
        <circle cx="100" cy="90" r="20" fill="var(--card)" />
        <circle cx="100" cy="90" r="17" fill={c} />
        <path d="M91 90 l6 6 12 -13" fill="none" stroke="white" strokeWidth="3.4" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    </Blob>
  );
}

/** Notifications feature — a bell with radiating sound arcs and a small unread badge. */
export function NotificationsIllustration({ className, tone = "primary" }: { className?: string; tone?: "primary" | "accent" }) {
  const c = tone === "accent" ? "var(--brand-accent, var(--primary))" : "var(--primary)";
  return (
    <Blob tone={tone} className={className}>
      <svg viewBox="0 0 140 120" className="w-[60%] h-[60%]">
        <path d="M40 78 c0 -4 4 -6 4 -14 v-10 a26 26 0 0 1 52 0 v10 c0 8 4 10 4 14 Z" fill="var(--card)" stroke={c} strokeWidth="2.5" strokeLinejoin="round" />
        <path d="M60 86 a10 10 0 0 0 20 0" fill="none" stroke={c} strokeWidth="2.5" strokeLinecap="round" />
        <path d="M100 40 a34 34 0 0 1 4 30" fill="none" stroke={c} strokeOpacity="0.35" strokeWidth="3" strokeLinecap="round" />
        <path d="M110 30 a48 48 0 0 1 6 42" fill="none" stroke={c} strokeOpacity="0.2" strokeWidth="3" strokeLinecap="round" />
        <circle cx="88" cy="30" r="7" fill="var(--destructive)" />
      </svg>
    </Blob>
  );
}

/** How-it-works step 1 — a clipboard/form with a checklist and a pencil, mid-fill. */
export function FormIllustration({ className, tone = "primary" }: { className?: string; tone?: "primary" | "accent" }) {
  const c = tone === "accent" ? "var(--brand-accent, var(--primary))" : "var(--primary)";
  return (
    <Blob tone={tone} className={className}>
      <svg viewBox="0 0 140 120" className="w-[62%] h-[62%]">
        <rect x="34" y="18" width="72" height="88" rx="8" fill="var(--card)" stroke="var(--border)" strokeWidth="2" />
        <rect x="56" y="12" width="28" height="14" rx="4" fill={c} />
        {[0, 1, 2].map((row) => (
          <g key={row}>
            <rect x="46" y={42 + row * 18} width="12" height="12" rx="3" fill="none" stroke={c} strokeWidth="2" />
            {row < 2 && <path d={`M48.5 ${48 + row * 18} l2.5 2.5 l5 -5.5`} stroke={c} strokeWidth="2" fill="none" strokeLinecap="round" strokeLinejoin="round" />}
            <rect x="64" y={45 + row * 18} width="30" height="4" rx="2" fill="var(--muted-foreground)" opacity={row < 2 ? 0.4 : 0.25} />
          </g>
        ))}
      </svg>
      <div className="absolute -bottom-1 -right-1 w-11 h-11 rounded-2xl flex items-center justify-center border -rotate-6"
        style={{ background: c, borderColor: "var(--card)" }}>
        <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="m18 2 4 4-13 13-5 1 1-5Z" />
        </svg>
      </div>
    </Blob>
  );
}

/** How-it-works step 2 — a rocket ascending with a soft exhaust trail and a couple of stars. */
export function LaunchIllustration({ className, tone = "primary" }: { className?: string; tone?: "primary" | "accent" }) {
  const c = tone === "accent" ? "var(--brand-accent, var(--primary))" : "var(--primary)";
  return (
    <Blob tone={tone} className={className}>
      <svg viewBox="0 0 140 120" className="w-[58%] h-[58%]">
        <path d="M70 12 c14 14 16 34 12 52 l-24 0 c-4 -18 -2 -38 12 -52 Z" fill={c} />
        <circle cx="70" cy="38" r="7" fill="var(--card)" />
        <path d="M46 64 l12 -4 v18 z" fill={c} opacity="0.75" />
        <path d="M94 64 l-12 -4 v18 z" fill={c} opacity="0.75" />
        <path d="M62 66 h16 l-5 22 a3 3 0 0 1 -6 0 Z" fill={c} opacity="0.9" />
        <path d="M62 92 c-4 8 -4 14 0 20 c4 -6 4 -12 0 -20 Z" fill={c} opacity="0.35" />
        <path d="M78 92 c4 8 4 14 0 20 c-4 -6 -4 -12 0 -20 Z" fill={c} opacity="0.5" />
        <circle cx="104" cy="26" r="2.4" fill={c} opacity="0.6" />
        <circle cx="30" cy="42" r="2" fill={c} opacity="0.5" />
      </svg>
    </Blob>
  );
}

/** How-it-works step 3 — several avatar circles converging toward a central "join" hub. */
export function JoinIllustration({ className, tone = "primary" }: { className?: string; tone?: "primary" | "accent" }) {
  const c = tone === "accent" ? "var(--brand-accent, var(--primary))" : "var(--primary)";
  return (
    <Blob tone={tone} className={className}>
      <svg viewBox="0 0 140 120" className="w-[68%] h-[68%]">
        {[
          [30, 30], [110, 30], [24, 78], [116, 78],
        ].map(([x, y], i) => (
          <g key={i}>
            <line x1={x} y1={y} x2="70" y2="62" stroke={c} strokeOpacity="0.25" strokeWidth="2" strokeDasharray="1 6" strokeLinecap="round" />
            <circle cx={x} cy={y} r="11" fill="var(--card)" stroke="var(--border)" strokeWidth="2" />
            <circle cx={x} cy={y - 2.5} r="3.6" fill={c} opacity="0.7" />
            <path d={`M${x - 6} ${y + 8} a6 5 0 0 1 12 0`} fill={c} opacity="0.7" />
          </g>
        ))}
        <circle cx="70" cy="62" r="18" fill={c} />
        <path d="M70 53v18M61 62h18" stroke="white" strokeWidth="2.6" strokeLinecap="round" />
      </svg>
    </Blob>
  );
}

/** How-it-works step 4 — a shield with a checkmark, small control toggles beside it. */
export function ControlIllustration({ className, tone = "primary" }: { className?: string; tone?: "primary" | "accent" }) {
  const c = tone === "accent" ? "var(--brand-accent, var(--primary))" : "var(--primary)";
  return (
    <Blob tone={tone} className={className}>
      <svg viewBox="0 0 140 120" className="w-[62%] h-[62%]">
        <path d="M70 12 l30 11 v26 c0 24 -14 40 -30 49 c-16 -9 -30 -25 -30 -49 V23 Z" fill="var(--card)" stroke={c} strokeWidth="2.5" strokeLinejoin="round" />
        <path d="M56 52 l10 10 l20 -22" stroke={c} strokeWidth="4" fill="none" strokeLinecap="round" strokeLinejoin="round" />
        {[0, 1].map((i) => (
          <g key={i} transform={`translate(0 ${i * 20})`}>
            <rect x="14" y="86" width="24" height="12" rx="6" fill={i === 0 ? c : "var(--muted)"} opacity={i === 0 ? 0.85 : 1} />
            <circle cx={i === 0 ? 32 : 22} cy="92" r="5" fill="var(--card)" />
          </g>
        ))}
      </svg>
    </Blob>
  );
}

/** WhatsApp-comparison section — a scattered, disconnected chat bubble cluster (the chaos of coordinating over chat) fading into a single organized node. */
export function ScatteredChatIllustration({ className }: { className?: string }) {
  return (
    <Blob tone="primary" className={className}>
      <svg viewBox="0 0 140 120" className="w-[72%] h-[72%]">
        {[
          { x: 18, y: 20, w: 30, h: 20, r: -8 },
          { x: 66, y: 14, w: 26, h: 18, r: 6 },
          { x: 96, y: 40, w: 28, h: 19, r: -4 },
          { x: 20, y: 52, w: 24, h: 17, r: 5 },
        ].map((b, i) => (
          <g key={i} transform={`rotate(${b.r} ${b.x + b.w / 2} ${b.y + b.h / 2})`} opacity="0.55">
            <rect x={b.x} y={b.y} width={b.w} height={b.h} rx="7" fill="var(--muted)" stroke="var(--border)" />
            <path d={`M${b.x + 8} ${b.y + b.h} l-4 6 l8 -2 z`} fill="var(--muted)" stroke="var(--border)" />
          </g>
        ))}
        <line x1="30" y1="30" x2="60" y2="90" stroke="var(--destructive)" strokeOpacity="0.3" strokeWidth="2" strokeLinecap="round" />
        <line x1="90" y1="90" x2="60" y2="90" stroke="var(--destructive)" strokeOpacity="0.3" strokeWidth="2" strokeLinecap="round" />
        <line x1="90" y1="90" x2="60" y2="30" stroke="var(--destructive)" strokeOpacity="0.15" strokeWidth="2" strokeLinecap="round" />
        <circle cx="60" cy="90" r="16" fill="var(--card)" stroke="var(--primary)" strokeWidth="2" />
        <path d="M53 90 l5 5 l10 -11" stroke="var(--primary)" strokeWidth="2.4" fill="none" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    </Blob>
  );
}
