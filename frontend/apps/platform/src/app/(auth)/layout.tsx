import Link from "next/link";
import { RedirectIfAuthenticated } from "@/components/platform/redirect-if-authenticated";

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen bg-background">
      {/* ── Left — branding panel ── */}
      <aside
        className="hidden md:flex md:w-[42%] lg:w-[46%] relative flex-col justify-center overflow-hidden px-10 lg:px-14 py-10"
        style={{
          background: `
            radial-gradient(120% 80% at 100% -10%, color-mix(in oklch, var(--primary) 45%, transparent) 0%, transparent 55%),
            radial-gradient(110% 70% at -10% 110%, color-mix(in oklch, var(--primary) 25%, black) 0%, transparent 60%),
            color-mix(in oklch, var(--sidebar) 85%, black)
          `,
        }}
      >
        {/* Dot-grid texture — the same kind of quiet depth a flat fill can't give */}
        <div
          className="pointer-events-none absolute inset-0 opacity-[0.12]"
          style={{ backgroundImage: "radial-gradient(rgba(255,255,255,0.7) 1px, transparent 1px)", backgroundSize: "26px 26px" }}
        />

        <div className="relative space-y-10 w-full">
          <Link href="/" className="flex items-center gap-3 w-fit transition-opacity hover:opacity-80">
            <div className="h-9 w-9 rounded-lg flex items-center justify-center bg-primary">
              <span className="text-[13px] font-bold text-white tracking-tight">P</span>
            </div>
            <span className="text-[15px] font-semibold tracking-wide uppercase" style={{ color: "rgba(255,255,255,0.75)" }}>
              Platform Portal
            </span>
          </Link>

          {/* Headline + body — spans most of the panel's width instead of a
              narrow fixed column, so it fills the section instead of hugging
              the left edge with dead space beside it. */}
          <div className="max-w-[92%] xl:max-w-[480px] space-y-5">
            <h1
              className="font-[family-name:var(--font-display)] leading-[1.12]"
              style={{ fontSize: "clamp(2.5rem, 4vw, 3.5rem)", fontWeight: 700, letterSpacing: "-0.02em", color: "#fff" }}
            >
              Onboard institutions. Keep every tenant healthy.
            </h1>
            <p className="text-[18px] font-medium leading-relaxed max-w-[440px]" style={{ color: "rgba(255,255,255,0.7)" }}>
              Provision new institutions, track platform revenue, and support every tenant from one governance workspace.
            </p>
          </div>

          <ul className="space-y-4">
            {[
              "Onboard and provision institutions",
              "Track institution activity and platform revenue",
              "Support and audit every tenant",
            ].map((item) => (
              <li key={item} className="flex items-center gap-3.5">
                <div className="w-6 h-6 rounded-full flex items-center justify-center shrink-0" style={{ background: "rgba(255,255,255,0.15)" }}>
                  <svg width="12" height="10" viewBox="0 0 10 8" fill="none">
                    <path d="M1 4l2.5 2.5L9 1" stroke="white" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                  </svg>
                </div>
                <span className="text-[16px] font-medium" style={{ color: "rgba(255,255,255,0.85)" }}>{item}</span>
              </li>
            ))}
          </ul>

          {/* Footer line — plain and honest for an internal tool, no fabricated testimonial */}
          <div className="border-t pt-6" style={{ borderColor: "rgba(255,255,255,0.15)" }}>
            <p className="text-[13px] font-medium" style={{ color: "rgba(255,255,255,0.5)" }}>
              Internal tool &middot; Platform staff access only
            </p>
          </div>
        </div>
      </aside>

      {/* ── Right — form panel ── */}
      <main className="flex-1 flex items-center justify-center p-6 sm:p-8 md:p-12 overflow-auto">
        <div className="w-full max-w-[420px]">
          {/* Mobile-only brand mark — the aside above is hidden below md, so
              small screens need their own compact header, matching member
              and institution's auth pages. */}
          <Link href="/" className="mb-8 md:hidden flex flex-col items-center gap-3 transition-opacity hover:opacity-80">
            <div className="h-12 w-12 rounded-xl flex items-center justify-center bg-primary">
              <span className="text-[16px] font-bold text-white tracking-tight">P</span>
            </div>
            <p className="text-[15px] font-semibold" style={{ color: "var(--foreground)" }}>Platform Portal</p>
          </Link>
          <RedirectIfAuthenticated>{children}</RedirectIfAuthenticated>
        </div>
      </main>
    </div>
  );
}
