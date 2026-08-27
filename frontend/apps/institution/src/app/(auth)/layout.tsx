import Link from "next/link";
import { getInitials } from "@alumni/ui";
import { getInstitutionTheme } from "@/lib/theme";
import { RedirectIfAuthenticated } from "@/components/institution/redirect-if-authenticated";

export default async function AuthLayout({ children }: { children: React.ReactNode }) {
  const theme = await getInstitutionTheme();
  // Only a real institution name can be dropped into "Run X's alumni
  // office..." — the generic fallback ("your institution") reads fine
  // standalone but odd once interpolated, so branch instead of always
  // reusing the same template.
  const institutionName = theme?.displayName;
  const displayName = institutionName || "your institution";
  const headline = theme?.authHeadline || (institutionName
    ? `Run ${institutionName}'s alumni office with a clear view of what matters.`
    : "Run your alumni office with a clear view of what matters.");
  const subtext = theme?.authSubtext || (institutionName
    ? `Approve members, keep membership current, publish opportunities, and steward every contribution from one trusted ${institutionName} workspace.`
    : "Approve members, keep membership current, publish opportunities, and steward every contribution from one trusted workspace.");
  const markImage = theme?.iconUrl || theme?.logoUrl;

  return (
    <div className="flex min-h-screen bg-background selection:bg-primary/10">

      {/* ── Left — branding panel ── */}
      <aside
        className="hidden md:flex md:w-[42%] lg:w-[46%] relative flex-col justify-center overflow-hidden px-10 lg:px-14 py-10"
        style={{
          background: `
            radial-gradient(120% 80% at 100% -10%, color-mix(in oklch, var(--primary) 55%, transparent) 0%, transparent 55%),
            radial-gradient(110% 70% at -10% 110%, color-mix(in oklch, var(--primary) 35%, black) 0%, transparent 60%),
            color-mix(in oklch, var(--primary) 78%, black)
          `,
        }}
      >
        {/* Dot-grid texture — the same kind of quiet depth a flat brand-color fill can't give */}
        <div
          className="pointer-events-none absolute inset-0 opacity-[0.12]"
          style={{ backgroundImage: "radial-gradient(rgba(255,255,255,0.7) 1px, transparent 1px)", backgroundSize: "26px 26px" }}
        />

        <div className="relative space-y-10 w-full">
          {/* Logo mark — the institution's own icon, or an initials badge as fallback */}
          <Link href="/" className="flex items-center gap-3 w-fit transition-opacity hover:opacity-80">
            {markImage ? (
              <img src={markImage} alt={displayName} className="h-9 w-9 rounded-lg object-cover" />
            ) : (
              <div
                className="h-9 w-9 rounded-lg flex items-center justify-center"
                style={{ background: "rgba(255,255,255,0.12)" }}
              >
                <span className="text-[13px] font-bold text-white tracking-tight">{getInitials(displayName)}</span>
              </div>
            )}
            <span className="text-[15px] font-semibold tracking-wide uppercase" style={{ color: "rgba(255,255,255,0.75)" }}>
              {theme?.displayName || "Institution Portal"}
            </span>
          </Link>

          {/* Headline + body — spans most of the panel's width instead of a
              narrow fixed column, so it fills the section instead of hugging
              the left edge with dead space beside it. */}
          <div className="max-w-[92%] xl:max-w-[480px] space-y-5">
            <h1
              className="leading-[1.12]"
              style={{ fontSize: "clamp(2.5rem, 4vw, 3.5rem)", fontWeight: 700, letterSpacing: "-0.02em", color: "#fff" }}
            >
              {headline}
            </h1>
            <p
              className="text-[18px] font-medium leading-relaxed max-w-[440px]"
              style={{ color: "rgba(255,255,255,0.7)" }}
            >
              {subtext}
            </p>
          </div>

          {/* Feature list */}
          <ul className="space-y-4">
            {[
              "Approve member registrations",
              "Monitor activity & contributions",
              "Publish events and announcements",
            ].map(item => (
              <li key={item} className="flex items-center gap-3.5">
                <div
                  className="w-6 h-6 rounded-full flex items-center justify-center shrink-0"
                  style={{ background: "rgba(255,255,255,0.15)" }}
                >
                  <svg width="12" height="10" viewBox="0 0 10 8" fill="none">
                    <path d="M1 4l2.5 2.5L9 1" stroke="white" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
                  </svg>
                </div>
                <span className="text-[16px] font-medium" style={{ color: "rgba(255,255,255,0.85)" }}>
                  {item}
                </span>
              </li>
            ))}
          </ul>

          {/* Footer line */}
          <div className="border-t pt-6" style={{ borderColor: "rgba(255,255,255,0.15)" }}>
            <p className="text-[13px] font-medium" style={{ color: "rgba(255,255,255,0.5)" }}>
              {institutionName ? `${institutionName} alumni workspace` : "Staff workspace"} &middot; Staff access only
            </p>
          </div>
        </div>
      </aside>

      {/* ── Right — form panel ── */}
      <main className="flex-1 flex items-center justify-center p-6 sm:p-10 md:p-14 overflow-auto">
        <div className="w-full max-w-[520px]">
          <RedirectIfAuthenticated>{children}</RedirectIfAuthenticated>
        </div>
      </main>

    </div>
  );
}
