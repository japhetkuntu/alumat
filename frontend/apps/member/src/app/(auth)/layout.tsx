import { getInitials } from "@alumni/ui";
import { getInstitutionTheme } from "@/lib/theme";
import { RedirectIfAuthenticated } from "@/components/member/redirect-if-authenticated";

export default async function AuthLayout({ children }: { children: React.ReactNode }) {
  const theme = await getInstitutionTheme();
  const displayName = theme?.displayName || "your alumni network";
  const headline = theme?.authHeadline || `Welcome to the ${displayName} alumni network.`;
  const subtext = theme?.authSubtext ||
    `Stay connected with fellow graduates, find opportunities, and support ${displayName} — all from one place built for you.`;
  const markImage = theme?.iconUrl || theme?.logoUrl;

  return (
    <div className="flex min-h-screen bg-background selection:bg-primary/10">

      {/* ── Left — branding panel ── */}
      <aside
        className="hidden md:flex md:w-[42%] lg:w-[46%] flex-col justify-between px-10 lg:px-14 py-10"
        style={{ background: "var(--primary)" }}
      >
        <div className="space-y-10">
          {/* Logo mark — the institution's own icon, or an initials badge as fallback */}
          <div className="flex items-center gap-3">
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
            <span className="text-[13.5px] font-semibold tracking-wide uppercase" style={{ color: "rgba(255,255,255,0.75)" }}>
              {theme?.displayName || "Alumni Portal"}
            </span>
          </div>

          {/* Headline + body */}
          <div className="max-w-[340px] space-y-4">
            <h1
              className="leading-[1.1]"
              style={{ fontSize: "clamp(2rem, 3.5vw, 2.75rem)", fontWeight: 700, letterSpacing: "-0.02em", color: "#fff" }}
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
              "Find and reconnect with classmates",
              "Get first access to jobs and mentorship",
              "Support fundraisers that matter to you",
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

        {/* Bottom: footer line */}
        <div
          className="border-t pt-6"
          style={{ borderColor: "rgba(255,255,255,0.15)" }}
        >
          <p className="text-[11px]" style={{ color: "rgba(255,255,255,0.4)" }}>
            {displayName} alumni network
          </p>
        </div>
      </aside>

      {/* ── Right — form panel ── */}
      <main className="flex-1 flex items-center justify-center p-6 sm:p-10 md:p-14 overflow-auto">
        <div className="w-full max-w-[480px]">
          <RedirectIfAuthenticated>{children}</RedirectIfAuthenticated>
        </div>
      </main>

    </div>
  );
}
