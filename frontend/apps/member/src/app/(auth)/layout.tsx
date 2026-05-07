export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="auth-page-root flex min-h-screen bg-background selection:bg-primary/10">
      {/* Left — member branding panel */}
      <aside className="hidden md:flex md:w-[40%] lg:w-[45%] flex-col justify-between px-6 md:px-10 py-8 md:py-10 bg-[var(--umat-emerald)] text-white">
        <div className="space-y-8">
          <div className="flex items-center gap-3">
            <div className="h-10 w-10 rounded-xl bg-white/10 flex items-center justify-center">
              <span className="text-lg font-bold text-white">UM</span>
            </div>
            <span className="text-sm font-semibold tracking-wide text-white/80">UMaT Alumni</span>
          </div>

          <div className="max-w-sm space-y-5">
            <h1 className="text-[52px] font-black leading-[1.1]">Opportunities, Community, and Growth — in one place.</h1>
            <p className="text-[16px] font-medium text-white/70">
              A single hub for alumni events, networks, and career-building resources.
            </p>
            <ul className="space-y-2 text-[15px] text-white/80">
              <li className="flex items-start gap-2">
                <span className="mt-1 text-white/70">✓</span>
                Connect with fellow graduates
              </li>
              <li className="flex items-start gap-2">
                <span className="mt-1 text-white/70">✓</span>
                Discover events & opportunities
              </li>
              <li className="flex items-start gap-2">
                <span className="mt-1 text-white/70">✓</span>
                Share updates and access resources
              </li>
            </ul>
          </div>
        </div>

        <div className="text-sm text-white/60">
          <p className="font-semibold text-white">&quot;A simple, reliable space to keep our community connected.&quot;</p>
          <p className="mt-2">— Esther N., Class of 2016</p>
        </div>
      </aside>

      {/* Right — form panel */}
      <main className="flex-1 overflow-y-auto p-4 sm:p-6 md:p-10 bg-background md:flex md:items-center md:justify-center">
        <div className="w-full max-w-[520px] mx-auto py-6 md:py-0">
          {children}
        </div>
      </main>
    </div>
  );
}
