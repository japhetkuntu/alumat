"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import {
  LayoutDashboard,
  Users,
  Megaphone,
  CreditCard,
  Briefcase,
  Calendar,
  Newspaper,
  MessageSquare,
  GraduationCap,
  FolderOpen,
  BarChart3,
  Settings,
  Menu,
  ShieldCheck,
  Star,
  Bell,
  LifeBuoy,
  X,
  Layers,
  Users2,
  Radio,
  ShoppingBag,
  Images,
  Store,
} from "@alumni/ui";
import { useState, useEffect, useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { cn } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { useHostname } from "@/hooks/use-hostname";
import { Avatar, AvatarFallback } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { getInitials } from "@alumni/ui";
import { NotificationPanel } from "@/components/institution/notification-panel";
import { institutionClient } from "@/lib/api-client";
import { GPU_LAYER_STYLE } from "@/lib/gpu-layer-style";

// Maps each gateable nav item to the backend feature key that can disable it
// (see InstitutionFeatures in ReservEase.Alumni.PostgresDb.Sdk). Items not
// listed here are core plumbing and can never be disabled.
const NAV_FEATURE_KEYS: Record<string, string> = {
  "/campaigns": "Contributions",
  "/membership": "Contributions",
  "/contributions": "Contributions",
  "/jobs": "Jobs",
  "/events": "Events",
  "/news": "News",
  "/forum": "Forum",
  "/mentorship": "Mentorship",
  "/resources": "Resources",
  "/spotlights": "Spotlights",
  "/store": "Store",
  "/albums": "PhotoAlbums",
  "/business-directory": "BusinessDirectory",
};

// Grouped by job-to-be-done, money first — an institution's own admins care
// most about dues/campaign health day to day, so that's the first thing they
// see below the dashboard, not buried under people/community management.
const baseNavItems = [
  { href: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
  { label: "Money", isHeader: true },
  { href: "/campaigns", label: "Fundraisers", icon: Megaphone },
  { href: "/membership", label: "Dues", icon: CreditCard },
  { href: "/contributions", label: "Contributions", icon: CreditCard },
  { href: "/store", label: "Store", icon: ShoppingBag },
  { label: "People", isHeader: true },
  { href: "/members", label: "Members", icon: Users },
  { href: "/batches", label: "Batches", icon: Layers },
  { href: "/communities", label: "Communities", icon: Users2 },
  { label: "Community", isHeader: true },
  { href: "/jobs", label: "Jobs", icon: Briefcase },
  { href: "/events", label: "Events", icon: Calendar },
  { href: "/news", label: "News", icon: Newspaper },
  { href: "/forum", label: "Forum", icon: MessageSquare },
  { href: "/mentorship", label: "Mentorship", icon: GraduationCap },
  { href: "/resources", label: "Resources", icon: FolderOpen },
  { href: "/spotlights", label: "Spotlights", icon: Star },
  { href: "/albums", label: "Photo Albums", icon: Images },
  { href: "/business-directory", label: "Business Directory", icon: Store },
  { label: "Insights", isHeader: true },
  { href: "/reports", label: "Reports", icon: BarChart3 },
  { href: "/notifications", label: "Notifications", icon: Bell },
  { href: "/broadcast", label: "Broadcast", icon: Radio },
  { label: "Institution", isHeader: true },
  { href: "/settings", label: "Settings", icon: Settings },
  { href: "/support", label: "Support", icon: LifeBuoy },
];

export function AdminSidebar({ onClose }: { onClose?: () => void }) {
  const pathname = usePathname();
  const { user } = useAuth();
  // Cosmetic/structural only: reads the actual hostname this session is on.
  // Real per-subdomain tenant resolution against the backend is future work.
  const tenantHost = useHostname();

  const { data: theme } = useQuery({
    queryKey: ["institution-nav-theme"],
    queryFn: async () => {
      const res = await institutionClient.get<{ data: { disabledFeatures: string[]; portalName?: string; portalTitle?: string; logoUrl?: string | null; iconUrl?: string | null } }>("/public/institution/theme");
      return res.data.data;
    },
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
  const disabledFeatures = useMemo(() => new Set(theme?.disabledFeatures ?? []), [theme]);
  const portalBrandName = theme?.portalTitle || theme?.portalName || "Institution Portal";
  const brandMark = theme?.iconUrl || theme?.logoUrl;

  const navItems = useMemo(() => {
    const items = baseNavItems.filter((item) => {
      if (item.href === "/forum" || item.href === "/mentorship" || item.href === "/albums" || item.href === "/business-directory") {
        if (user?.role !== "SuperAdmin" && user?.role !== "Admin") return false;
      }
      if (item.href === "/store" && user?.role !== "SuperAdmin") return false;
      const featureKey = item.href ? NAV_FEATURE_KEYS[item.href] : undefined;
      if (featureKey && disabledFeatures.has(featureKey)) return false;
      return true;
    });

    if (user?.role === "SuperAdmin") {
      const peopleHeaderIndex = items.findIndex((item) => item.isHeader && item.label === "People");
      const insertAt = peopleHeaderIndex === -1 ? items.length : peopleHeaderIndex + 1;
      items.splice(insertAt, 0, { href: "/staff", label: "Institution Admins", icon: ShieldCheck });
    }

    // Drop any header whose group ended up with zero visible items (e.g. an
    // institution disables every feature in a whole section).
    return items.filter((item, idx) => {
      if (!item.isHeader) return true;
      const next = items[idx + 1];
      return !!next && !next.isHeader;
    });
  }, [user?.role, disabledFeatures]);

  return (
    <div className="flex flex-col h-full bg-sidebar text-sidebar-foreground w-[248px] select-none">
      <div className="px-4 pt-5 pb-4 border-b border-sidebar-border">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2.5 min-w-0">
            {brandMark ? (
              <img src={brandMark} alt={portalBrandName} className="w-8 h-8 rounded-md object-cover shrink-0 border border-white/10 shadow-sm" />
            ) : (
              <div className="w-8 h-8 rounded-md bg-primary flex items-center justify-center text-white text-[14px] font-bold shrink-0">
                {portalBrandName.charAt(0).toUpperCase()}
              </div>
            )}
            <div className="overflow-hidden">
              <p className="font-bold text-[15px] tracking-tight truncate text-white leading-tight">{portalBrandName}</p>
            </div>
          </div>
          {onClose && (
            <Button variant="ghost" size="sm" className="h-8 w-8 p-0 text-sidebar-foreground hover:bg-white/10 hover:text-white" onClick={onClose} aria-label="Close sidebar">
              <X size={14} />
            </Button>
          )}
        </div>
        <div className="mt-3 flex items-center gap-1.5 text-[11px]">
          {tenantHost && (
            <span className="font-mono text-blue-300/90 truncate" title={tenantHost}>{tenantHost}</span>
          )}
          <span className="text-slate-500">&middot;</span>
          <span className="text-slate-400 shrink-0">{user?.role ?? "Admin"}</span>
        </div>
      </div>

      <nav className="flex-1 overflow-y-auto px-3 py-3 custom-scrollbar">
        {navItems.map((item, i) => {
          if (item.isHeader) {
            return (
              <div key={i} className="flex items-center gap-2 px-3 pt-6 pb-1.5 first:pt-1">
                <span className="h-px w-2.5 bg-white/15 shrink-0" />
                <p className="text-[10px] font-bold text-slate-500 uppercase tracking-[0.14em]">{item.label}</p>
              </div>
            );
          }

          const active = pathname === item.href || pathname.startsWith(item.href + "/");
          const isHome = item.href === "/dashboard";
          return (
            <Link
              key={item.href}
              href={item.href!}
              onClick={onClose}
              className={cn(
                "flex items-center gap-2.5 px-3 rounded-md transition-all duration-150 ease-[cubic-bezier(0.2,0,0,1)] group relative mb-0.5",
                isHome ? "py-2.5 mb-2 text-[13.5px] font-semibold" : "py-2 text-[13px] font-medium",
                active
                  ? "bg-sidebar-accent text-sidebar-accent-foreground"
                  : "text-sidebar-foreground hover:bg-white/5 hover:text-white active:scale-[0.99]"
              )}
            >
              {item.icon && <item.icon size={isHome ? 17 : 16} className="shrink-0" strokeWidth={active || isHome ? 2.25 : 2} />}
              {item.label}
            </Link>
          );
        })}
      </nav>

      <div className="p-4 mt-auto border-t border-sidebar-border">
        <div className="flex items-center gap-3 mb-4 px-1">
          <Avatar className="h-8 w-8 ring-2 ring-sidebar shadow-md">
            <AvatarFallback name={user?.name} className="text-[10px]">{getInitials(user?.name ?? "A")}</AvatarFallback>
          </Avatar>
          <div className="overflow-hidden flex-1">
            <p className="text-[12px] font-bold truncate leading-tight text-white">{user?.name ?? "Administrator"}</p>
            <p className="text-[10px] text-blue-300 font-bold uppercase tracking-wider">{user?.role ?? "Super Admin"}</p>
          </div>
        </div>
      </div>
    </div>
  );
}

function useCurrentPageTitle() {
  const pathname = usePathname();
  return useMemo(() => {
    const match = baseNavItems.find((item) => !item.isHeader && item.href && (pathname === item.href || pathname.startsWith(item.href + "/")));
    return match?.label ?? "Dashboard";
  }, [pathname]);
}

export function InstitutionLayout({ children }: { children: React.ReactNode }) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const { isLoading, isAdmin, user } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const pageTitle = useCurrentPageTitle();

  useEffect(() => {
    if (!isLoading && !isAdmin && pathname !== "/login") {
      router.replace("/login");
    }
  }, [isLoading, isAdmin, pathname, router]);

  if (isLoading || !isAdmin) return null;

  return (
    <div className="flex h-screen bg-background overflow-hidden selection:bg-primary/10">
      {/* Desktop sidebar */}
      <div className="hidden lg:flex shrink-0">
        <AdminSidebar />
      </div>

      {/* Mobile overlay */}
      {mobileOpen && (
        <div className="fixed inset-0 z-[9999] flex lg:hidden">
          <div className="fixed inset-0 bg-black/40 backdrop-blur-sm animate-in fade-in duration-300" onClick={() => setMobileOpen(false)} />
          <div className="relative z-10 animate-in slide-in-from-left duration-500 shadow-2xl">
            <AdminSidebar onClose={() => setMobileOpen(false)} />
          </div>
        </div>
      )}

      <div className="flex-1 flex flex-col min-w-0 bg-background relative">
        {/* Desktop header — matches the Platform Portal's translucent topbar */}
        <div className="hidden lg:flex items-center justify-between px-6 h-14 border-b border-border bg-background/80 backdrop-blur-xl sticky top-0 z-40">
          <p className="text-[14px] font-semibold text-foreground tracking-tight truncate">{pageTitle}</p>
          <div className="flex items-center gap-4 shrink-0">
            <NotificationPanel />
            <span className="text-[13px] font-semibold">
              {user?.name ?? "Staff"} <span className="text-muted-foreground font-normal">&middot; {user?.role ?? "Admin"}</span>
            </span>
          </div>
        </div>

        {/* Mobile header — `fixed`, not `sticky`: this div's actual ancestor
            is NOT a scrolling container (that's <main>, a sibling below
            it), so `sticky` never had a scroll box to stick within — on iOS
            Safari's dynamic viewport (address bar collapsing/expanding),
            that's exactly what let the header scroll away with the page
            instead of staying put. `fixed` pins it to the true viewport
            regardless. GPU_LAYER_STYLE avoids a WebKit repaint bug where a
            fixed + backdrop-blur layer can fail to redraw after a sibling
            blur layer (the mobile drawer overlay) unmounts. */}
        <div
          className="lg:hidden fixed top-0 left-0 right-0 z-40 flex items-center justify-between px-4 h-14 border-b border-border bg-background/80 backdrop-blur-xl"
          style={{ paddingTop: 'env(safe-area-inset-top)', ...GPU_LAYER_STYLE }}
        >
          <div className="flex items-center gap-3">
            <Button variant="ghost" size="sm" className="h-9 w-9 p-0 rounded-lg hover:bg-muted -ml-1" onClick={() => setMobileOpen(true)} aria-label="Open menu">
              <Menu size={20} />
            </Button>
            <span className="font-bold text-[15px] tracking-tight truncate">{pageTitle}</span>
          </div>
          <div className="flex items-center gap-2">
            <NotificationPanel />
            <div className="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center border border-primary/20">
              <span className="text-[10px] font-bold text-primary">AD</span>
            </div>
          </div>
        </div>

        <main className="flex-1 overflow-y-auto bg-background selection:bg-primary/20 relative pt-14 lg:pt-0">
          <div className="max-w-[1800px] mx-auto min-h-full">
            {children}
          </div>
        </main>
      </div>
    </div>
  );
}
