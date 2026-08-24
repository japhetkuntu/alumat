import { getInstitutionTheme } from "@/lib/theme";

export default async function AuthLayout({ children }: { children: React.ReactNode }) {
  const theme = await getInstitutionTheme();
  const headline = theme?.authHeadline || "Run the alumni office with a clear view of what matters.";
  const subtext = theme?.authSubtext ||
    "Approve members, keep membership current, publish opportunities, and steward every contribution from one trusted institution workspace.";

  return (
    <div className="flex min-h-screen bg-background selection:bg-primary/10">

      {/* ── Left — branding panel ── */}
      <aside
        className="hidden md:flex md:w-[42%] lg:w-[46%] flex-col justify-between px-10 lg:px-14 py-10"
        style={{ background: "var(--sidebar)" }}
      >
        {/* Top: logo + headline */}
        <div className="space-y-10">
          {/* Logo mark */}
          <div className="flex items-center gap-3">
            <div
              className="h-9 w-9 rounded-lg flex items-center justify-center"
              style={{ background: "rgba(255,255,255,0.12)" }}
            >
              <span className="text-[13px] font-bold text-white tracking-tight">A</span>
            </div>
            <span className="text-[13.5px] font-semibold tracking-wide uppercase" style={{ color: "rgba(255,255,255,0.75)" }}>
              Institution Portal
            </span>
          </div>

          {/* Headline + body */}
          <div className="max-w-[340px] space-y-4">
            <h1
              className="leading-[1.1] text-white"
              style={{ fontSize: "clamp(2rem, 3.5vw, 2.75rem)", fontWeight: 700, letterSpacing: "-0.02em" }}
            >
              {headline}
            </h1>
            <p
              className="text-[15px] font-medium leading-relaxed"
              style={{ color: "rgba(255,255,255,0.65)" }}
            >
              {subtext}
            </p>
          </div>

          {/* Feature list */}
          <ul className="space-y-3">
            {[
              "Approve member registrations",
              "Monitor activity & contributions",
              "Publish events and announcements",
            ].map(item => (
              <li key={item} className="flex items-center gap-3">
                <div
                  className="w-5 h-5 rounded-full flex items-center justify-center shrink-0"
                  style={{ background: "rgba(255,255,255,0.15)" }}
                >
                  <svg width="10" height="8" viewBox="0 0 10 8" fill="none">
                    <path d="M1 4l2.5 2.5L9 1" stroke="white" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
                  </svg>
                </div>
                <span className="text-[14px]" style={{ color: "rgba(255,255,255,0.8)" }}>
                  {item}
                </span>
              </li>
            ))}
          </ul>
        </div>

        {/* Bottom: pull quote */}
        <div
          className="border-t pt-6 space-y-1"
          style={{ borderColor: "rgba(255,255,255,0.15)" }}
        >
          <p
            className="text-[14px] font-medium leading-relaxed"
            style={{ color: "rgba(255,255,255,0.85)" }}
          >
            &ldquo;Everything I need to keep our alumni network running smoothly.&rdquo;
          </p>
          <p className="text-[13px]" style={{ color: "rgba(255,255,255,0.5)" }}>
            — Ama A., Community Lead
          </p>
          <p className="text-[11px] pt-3" style={{ color: "rgba(255,255,255,0.4)" }}>
            Alumni workspace &middot; Staff access only
          </p>
        </div>
      </aside>

      {/* ── Right — form panel ── */}
      <main className="flex-1 flex items-center justify-center p-6 sm:p-10 md:p-14 overflow-auto">
        <div className="w-full max-w-[520px]">
          {children}
        </div>
      </main>

    </div>
  );
}
