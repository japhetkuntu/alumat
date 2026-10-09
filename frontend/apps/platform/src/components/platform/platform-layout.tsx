"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import {
  LayoutDashboard,
  Building2,
  CreditCard,
  FileText,
  TrendingUp,
  Users,
  LifeBuoy,
  Megaphone,
  ClipboardList,
  Settings,
  Menu,
  X,
  Inbox,
  GraduationCap,
  Target,
  Activity,
  ScrollText,
} from "@alumni/ui";
import { useEffect, useMemo, useState } from "react";
import { cn } from "@alumni/ui";
import { PortalShellSkeleton } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { Avatar, AvatarFallback } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { getInitials } from "@alumni/ui";
import { NotificationPanel } from "./notification-panel";
import { PlatformSetupChecklist } from "./setup-checklist";

const navItems = [
  { href: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
  { href: "/institutions", label: "Institutions", icon: Building2 },
  { href: "/activation", label: "Activation", icon: Target },
  { href: "/institution-health", label: "Institution health", icon: Activity },
  { href: "/members", label: "Members", icon: GraduationCap },
  { href: "/onboarding-leads", label: "Onboarding & Demos", icon: Inbox },
  { href: "/marketing-campaigns", label: "Marketing Campaigns", icon: Megaphone },
  { href: "/billing", label: "Payments & Revenue", icon: CreditCard },
  { href: "/analytics", label: "Analytics", icon: TrendingUp },
  { href: "/reports", label: "Reports", icon: FileText },
  { href: "/staff", label: "Platform Staff", icon: Users },
  { href: "/support", label: "Support", icon: LifeBuoy },
  { href: "/announcements", label: "Announcements", icon: Megaphone },
  { href: "/changelog", label: "Changelog", icon: ScrollText },
  { href: "/audit-log", label: "Audit Log", icon: ClipboardList },
  { href: "/settings", label: "Settings", icon: Settings },
];

export function PlatformSidebar({ onClose }: { onClose?: () => void }) {
  const pathname = usePathname();
  const { user, isSuperAdmin } = useAuth();
  const isSupport = user?.role === "Support";

  const visibleNavItems = navItems.filter((item) => {
    if (item.href === "/marketing-campaigns" && !isSuperAdmin && user?.role !== "Sales") return false;
    if ((item.href === "/staff" || item.href === "/audit-log") && !isSuperAdmin) return false;
    if (item.href === "/members" && !isSuperAdmin && !isSupport) return false;
    if (item.href === "/activation" && user?.role === "Billing") return false;
    if (item.href === "/institution-health" && !isSuperAdmin && !isSupport) return false;
    return true;
  });

  return (
    <div className="flex flex-col h-full bg-sidebar text-sidebar-foreground w-[248px] select-none">
      <div className="px-4 pt-5 pb-4 border-b border-sidebar-border flex items-center justify-between">
        <div className="flex flex-col gap-1.5 min-w-0">
          <img src="/alumunion-logo-white.svg" alt="AlumUnion" width={1490} height={405} className="h-9 w-auto object-contain object-left" />
          <span className="font-semibold text-[12px] tracking-[0.14em] uppercase" style={{ color: "rgba(255,255,255,0.65)" }}>Platform Portal</span>
        </div>
        {onClose && (
          <Button variant="ghost" size="sm" className="h-8 w-8 p-0 text-sidebar-foreground hover:bg-white/10 hover:text-white" onClick={onClose} aria-label="Close sidebar">
            <X size={14} />
          </Button>
        )}
      </div>

      <nav className="flex-1 overflow-y-auto px-3 py-3 custom-scrollbar">
        {visibleNavItems.map((item) => {
          const active = pathname === item.href || pathname.startsWith(item.href + "/");
          const isHome = item.href === "/dashboard";
          return (
            <Link
              key={item.href}
              href={item.href}
              onClick={onClose}
              className={cn(
                "flex items-center gap-2.5 px-3 rounded-md transition-all duration-150 ease-[cubic-bezier(0.2,0,0,1)] mb-0.5",
                isHome ? "py-2.5 mb-2 text-[13.5px] font-semibold" : "py-2 text-[13px] font-medium",
                active
                  ? "bg-sidebar-primary text-white"
                  : "text-sidebar-foreground hover:bg-white/5 hover:text-white active:scale-[0.99]"
              )}
            >
              <item.icon size={isHome ? 17 : 16} className="shrink-0" strokeWidth={active || isHome ? 2.25 : 2} />
              {item.label}
            </Link>
          );
        })}
      </nav>

      <div className="p-4 mt-auto border-t border-sidebar-border">
        <div className="flex items-center gap-3 mb-4 px-1">
          <Avatar className="h-8 w-8 ring-2 ring-sidebar shadow-md">
            <AvatarFallback name={user?.name} className="text-[12px]">{getInitials(user?.name ?? "P")}</AvatarFallback>
          </Avatar>
          <div className="overflow-hidden flex-1">
            <p className="text-[12px] font-bold truncate leading-tight text-white">{user?.name ?? "Platform staff"}</p>
            <p className="text-[12px] text-blue-300 font-bold uppercase tracking-wider">{user?.role ?? "SuperAdmin"}</p>
          </div>
        </div>
      </div>
    </div>
  );
}

function useCurrentPageTitle() {
  const pathname = usePathname();
  return useMemo(() => {
    const match = navItems.find((item) => pathname === item.href || pathname.startsWith(item.href + "/"));
    return match?.label ?? "Dashboard";
  }, [pathname]);
}

export function PlatformLayout({ children }: { children: React.ReactNode }) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const { isLoading, isPlatformStaff, user } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const pageTitle = useCurrentPageTitle();

  // Client pages can't export metadata, so the browser tab and history show the page name here.
  useEffect(() => {
    const base = document.title.split(" · ").pop() || "Alumni Portal";
    document.title = `${pageTitle} · ${base}`;
  }, [pageTitle]);

  useEffect(() => {
    if (!isLoading && !isPlatformStaff && pathname !== "/login") {
      router.replace("/login");
    }
  }, [isLoading, isPlatformStaff, pathname, router]);

  if (isLoading || !isPlatformStaff) return <PortalShellSkeleton sidebarWidth={248} />;

  return (
    <div className="flex h-screen bg-background overflow-hidden">
      <div className="hidden lg:flex shrink-0">
        <PlatformSidebar />
      </div>

      {mobileOpen && (
        <div className="fixed inset-0 z-[9999] flex lg:hidden">
          <div className="fixed inset-0 bg-black/40 backdrop-blur-sm" onClick={() => setMobileOpen(false)} />
          <div className="relative z-10 shadow-2xl">
            <PlatformSidebar onClose={() => setMobileOpen(false)} />
          </div>
        </div>
      )}

      <div className="flex-1 flex flex-col min-w-0 bg-background relative">
        <div className="hidden lg:flex items-center justify-between px-6 h-14 border-b border-border bg-background/80 backdrop-blur-xl sticky top-0 z-40">
          <p className="text-[14px] font-semibold text-foreground tracking-tight truncate">{pageTitle}</p>
          <div className="flex items-center gap-4 shrink-0">
            <NotificationPanel />
            <span className="text-[13px] font-semibold">
              {user?.name ?? "Platform staff"} <span className="text-muted-foreground font-normal">&middot; {user?.role ?? "SuperAdmin"}</span>
            </span>
          </div>
        </div>

        <div className="lg:hidden sticky top-0 z-10 flex items-center justify-between px-4 h-14 border-b border-border bg-background/80 backdrop-blur-xl">
          <div className="flex items-center gap-3">
            <Button variant="ghost" size="sm" className="h-9 w-9 p-0 rounded-lg hover:bg-muted" onClick={() => setMobileOpen(true)} aria-label="Open menu">
              <Menu size={20} />
            </Button>
            <span className="font-bold text-[15px] tracking-tight truncate">{pageTitle}</span>
          </div>
          <NotificationPanel />
        </div>

        <main className="flex-1 overflow-y-auto bg-background relative">
          <div className="max-w-[1500px] mx-auto min-h-full">
            {children}
          </div>
        </main>
      </div>
      <PlatformSetupChecklist />
    </div>
  );
}
