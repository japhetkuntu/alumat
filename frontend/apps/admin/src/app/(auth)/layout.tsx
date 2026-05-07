export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="auth-page-root flex min-h-screen bg-background selection:bg-primary/10">
      {/* Left — admin branding panel */}
      <aside className="hidden md:flex md:w-[40%] lg:w-[45%] flex-col justify-between px-6 md:px-10 py-8 md:py-10 bg-[var(--umat-emerald)] text-white">
        <div className="space-y-8">
          <div className="flex items-center gap-3">
            <div className="h-10 w-10 rounded-xl bg-white/10 flex items-center justify-center">
              <span className="text-lg font-bold text-white">UM</span>
            </div>
            <span className="text-sm font-semibold tracking-wide text-white/80">UMaT Admin</span>
          </div>

          <div className="max-w-sm space-y-5">
            <h1 className="text-[52px] font-black leading-[1.1]">Manage your platform. Empower your members.</h1>
            <p className="text-[16px] font-medium text-white/70">
              Run events, review registrations, and keep the community thriving from one dashboard.
            </p>
            <ul className="space-y-2 text-[15px] text-white/80">
              <li className="flex items-start gap-2">
                <span className="mt-1 text-white/70">✓</span>
                Approve member registrations
              </li>
              <li className="flex items-start gap-2">
                <span className="mt-1 text-white/70">✓</span>
                Monitor activity & contributions
              </li>
              <li className="flex items-start gap-2">
                <span className="mt-1 text-white/70">✓</span>
                Publish events and announcements
              </li>
            </ul>
          </div>
        </div>

        <div className="text-sm text-white/60">
          <p className="font-semibold text-white">&quot;Everything I need to keep our alumni network running smoothly.&quot;</p>
          <p className="mt-2">— Ama A., Community Lead</p>
        </div>
      </aside>

      {/* Right — form panel */}
      <main className="flex-1 flex items-center justify-center p-4 sm:p-6 md:p-10 overflow-hidden bg-background">
        <div className="w-full max-w-[520px] min-h-[520px] md:min-h-[480px] flex items-center justify-center">
          <div className="w-full px-4 sm:px-6 md:px-0">
            {children}
          </div>
        </div>
      </main>
    </div>
  );
}
