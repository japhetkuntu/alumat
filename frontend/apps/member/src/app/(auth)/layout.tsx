import Link from "next/link";
import { GraduationCap } from "lucide-react";
import { getInstitutionTheme } from "@/lib/theme";
import { RedirectIfAuthenticated } from "@/components/member/redirect-if-authenticated";

export default async function AuthLayout({ children }: { children: React.ReactNode }) {
  const theme = await getInstitutionTheme();
  const quote = theme?.authHeadline ||
    "My school didn't just give me a degree. It gave me a network that has opened every door since.";
  const quoteAttribution = theme?.authSubtext || "Alumna · Class of 2018";

  return (
    <div className="flex min-h-screen" style={{ background: "var(--background)" }}>

      {/* ── Left — photo panel ── */}
      <aside className="hidden md:flex md:w-[42%] lg:w-[48%] shrink-0 relative flex-col overflow-hidden">

        {/* Photo */}
        <img
          src="https://images.unsplash.com/photo-1523848309072-c199db53f137?auto=format&fit=crop&w=1400&q=85"
          alt="Aerial view of a mining site"
          className="absolute inset-0 w-full h-full object-cover"
        />

        {/* Overlay */}
        <div
          className="absolute inset-0"
          style={{
            background:
              "linear-gradient(160deg, rgba(0,0,0,0.6) 0%, rgba(0,0,0,0.25) 45%, rgba(0,0,0,0.8) 100%)",
          }}
        />

        {/* Content */}
        <div className="relative flex flex-col justify-between h-full p-10">

          {/* Logo */}
          <Link href="/" className="flex items-center gap-3 hover:opacity-80 transition-opacity">
            <div
              className="w-9 h-9 rounded-xl flex items-center justify-center shrink-0"
              style={{ background: "var(--primary)" }}
            >
              <GraduationCap size={17} color="white" />
            </div>
            <span className="text-[14px] font-semibold text-white tracking-tight">
              Alumni Portal
            </span>
          </Link>

          {/* Pull-quote — editorial signature element */}
          <div>
            <p
              className="font-[family-name:var(--font-display)] text-white/20 leading-none select-none mb-1"
              style={{ fontSize: "6rem", fontWeight: 700, lineHeight: 1 }}
              aria-hidden="true"
            >
              &ldquo;
            </p>
            <blockquote
              className="font-[family-name:var(--font-display)] text-white leading-tight mb-6"
              style={{
                fontSize: "clamp(1.3rem, 2vw, 1.75rem)",
                fontWeight: 600,
                letterSpacing: "-0.02em",
                maxWidth: "22ch",
              }}
            >
              {quote}
            </blockquote>
            <div className="flex items-center gap-3">
              <div
                className="w-[2px] self-stretch rounded-full"
                style={{ background: "var(--primary)" }}
              />
              <p className="text-[12px]" style={{ color: "rgba(255,255,255,0.6)" }}>
                {quoteAttribution}
              </p>
            </div>
           
          </div>

        </div>
      </aside>

      {/* ── Right — form panel ── */}
      <main
        className="flex flex-1 flex-col items-center justify-center overflow-y-auto px-6 py-12 sm:px-10"
        style={{ background: "var(--background)" }}
      >
        <div className="w-full max-w-[480px]">
          <RedirectIfAuthenticated>{children}</RedirectIfAuthenticated>
        </div>
      </main>

    </div>
  );
}
