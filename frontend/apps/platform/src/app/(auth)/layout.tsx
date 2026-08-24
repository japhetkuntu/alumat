export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen bg-background">
      {/* ── Left — branding panel ── */}
      <aside
        className="hidden md:flex md:w-[42%] lg:w-[46%] flex-col justify-between px-10 lg:px-14 py-10"
        style={{ background: "var(--sidebar)" }}
      >
        <div className="space-y-10">
          <div className="flex items-center gap-3">
            <div className="h-9 w-9 rounded-lg flex items-center justify-center bg-primary">
              <span className="text-[13px] font-bold text-white tracking-tight">P</span>
            </div>
            <span className="text-[13.5px] font-semibold tracking-wide uppercase" style={{ color: "rgba(255,255,255,0.75)" }}>
              Platform Portal
            </span>
          </div>

          <div className="max-w-[360px] space-y-4">
            <h1
              className="font-[family-name:var(--font-display)] leading-[1.1] text-white"
              style={{ fontSize: "clamp(2rem, 3.5vw, 2.75rem)", fontWeight: 700, letterSpacing: "-0.02em" }}
            >
              Onboard institutions. Keep every tenant healthy.
            </h1>
            <p className="text-[15px] font-medium leading-relaxed" style={{ color: "rgba(255,255,255,0.65)" }}>
              Provision new institutions, track subscriptions, and support every tenant from one governance workspace.
            </p>
          </div>

          <ul className="space-y-3">
            {[
              "Onboard and provision institutions",
              "Track subscriptions and plan usage",
              "Support and audit every tenant",
            ].map((item) => (
              <li key={item} className="flex items-center gap-3">
                <div className="w-5 h-5 rounded-full flex items-center justify-center shrink-0" style={{ background: "rgba(255,255,255,0.15)" }}>
                  <svg width="10" height="8" viewBox="0 0 10 8" fill="none">
                    <path d="M1 4l2.5 2.5L9 1" stroke="white" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                </div>
                <span className="text-[14px]" style={{ color: "rgba(255,255,255,0.8)" }}>{item}</span>
              </li>
            ))}
          </ul>
        </div>

        <div className="border-t pt-6 space-y-1" style={{ borderColor: "rgba(255,255,255,0.15)" }}>
          <p className="text-[14px] font-medium leading-relaxed" style={{ color: "rgba(255,255,255,0.85)" }}>
            &ldquo;One workspace to see every institution&apos;s health at a glance.&rdquo;
          </p>
          <p className="text-[13px]" style={{ color: "rgba(255,255,255,0.5)" }}>
            — Maya C., Platform Operations
          </p>
          <p className="text-[11px] pt-3" style={{ color: "rgba(255,255,255,0.4)" }}>
            Internal tool &middot; Platform staff access only
          </p>
        </div>
      </aside>

      <main className="flex-1 flex items-center justify-center p-6 sm:p-8 md:p-12 overflow-auto">
        <div className="w-full max-w-[420px]">{children}</div>
      </main>
    </div>
  );
}
