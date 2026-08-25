"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useState, useEffect, useMemo, ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/hooks/use-auth";
import { cn, getInitials } from "@alumni/ui";
import { Avatar, AvatarFallback } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { NotificationPanel } from "@/components/member/notification-panel";
import { memberClient } from "@/lib/api-client";
import { GPU_LAYER_STYLE } from "@/lib/gpu-layer-style";
import {
  LayoutDashboard,
  CreditCard,
  Briefcase,
  Calendar,
  Users,
  Newspaper,
  MessageSquare,
  GraduationCap,
  FolderOpen,
  UserCircle,
  Settings,
  Menu,
  X,
  Trophy,
  Star,
  UserPlus,
  StickyNote,
  UsersRound,
  Globe,
  CalendarDays,
} from "lucide-react";

// Grouped by what a member is trying to DO, not by feature type — keeps the
// sidebar scannable and gives "Home" a clear job (the daily-return feed)
// instead of competing for attention with 15 flat siblings.
const navGroups: { section: string | null; items: { href: string; label: string; icon: typeof LayoutDashboard }[] }[] = [
  {
    section: null,
    items: [
      { href: "/dashboard", label: "Home", icon: LayoutDashboard },
      { href: "/calendar", label: "Calendar", icon: CalendarDays },
    ],
  },
  {
    section: "Give",
    items: [
      { href: "/contributions", label: "Give", icon: CreditCard },
    ],
  },
  {
    section: "Grow",
    items: [
      { href: "/jobs", label: "Jobs", icon: Briefcase },
      { href: "/mentorship", label: "Mentorship", icon: GraduationCap },
      { href: "/directory", label: "Directory", icon: Users },
      { href: "/alumni-map", label: "Alumni Map", icon: Globe },
    ],
  },
  {
    section: "Community",
    items: [
      { href: "/class-notes", label: "Class Notes", icon: StickyNote },
      { href: "/forum", label: "Forum", icon: MessageSquare },
      { href: "/communities", label: "Communities", icon: UsersRound },
      { href: "/events", label: "Events", icon: Calendar },
      { href: "/news", label: "News", icon: Newspaper },
      { href: "/resources", label: "Resources", icon: FolderOpen },
    ],
  },
  {
    section: "Recognition",
    items: [
      { href: "/leaderboard", label: "Leaderboard", icon: Trophy },
      { href: "/spotlights", label: "Roll of Honour", icon: Star },
      { href: "/referrals", label: "Referrals", icon: UserPlus },
    ],
  },
  {
    section: null,
    items: [
      { href: "/profile", label: "Profile", icon: UserCircle },
    ],
  },
];
const navItems = navGroups.flatMap((g) => g.items);

// Maps each gateable nav item to the backend feature key that can disable it
// (see InstitutionFeatures in ReservEase.Alumni.PostgresDb.Sdk). Items not
// listed here are core plumbing and can never be disabled.
const NAV_FEATURE_KEYS: Record<string, string> = {
  "/contributions": "Contributions",
  "/jobs": "Jobs",
  "/events": "Events",
  "/directory": "Directory",
  "/alumni-map": "AlumniMap",
  "/calendar": "Calendar",
  "/news": "News",
  "/forum": "Forum",
  "/mentorship": "Mentorship",
  "/resources": "Resources",
  "/leaderboard": "Leaderboard",
  "/spotlights": "Spotlights",
  "/referrals": "Referrals",
  "/class-notes": "ClassNotes",
};

interface NavThemeData {
  disabledFeatures: string[];
  displayName?: string | null;
  logoUrl?: string | null;
  iconUrl?: string | null;
}

/** Shared across Sidebar, MobileBottomNav, and the mobile header — react-query dedupes the fetch since they all use the same queryKey. Exported so other screens (e.g. Settings) can show the real institution name instead of a generic placeholder. */
export function useNavTheme() {
  return useQuery({
    queryKey: ["member-nav-theme"],
    queryFn: async () => {
      const res = await memberClient.get<{ data: NavThemeData }>("/public/institution/theme");
      return res.data.data;
    },
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
}

function useDisabledFeatures(): Set<string> {
  const { data } = useNavTheme();
  return useMemo(() => new Set(data?.disabledFeatures ?? []), [data]);
}

function Sidebar({ onClose }: { onClose?: () => void }) {
  const pathname = usePathname();
  const { user } = useAuth();
  const { data: navTheme } = useNavTheme();
  const disabledFeatures = useDisabledFeatures();
  const brandName = navTheme?.displayName || "Alumni Portal";
  const brandMark = navTheme?.iconUrl || navTheme?.logoUrl;
  const visibleGroups = navGroups
    .map((g) => ({
      ...g,
      items: g.items.filter((item) => {
        const featureKey = NAV_FEATURE_KEYS[item.href];
        return !featureKey || !disabledFeatures.has(featureKey);
      }),
    }))
    .filter((g) => g.items.length > 0);
  let itemIndex = 0;

  return (
    <div className="flex flex-col h-full bg-sidebar w-[240px] border-r border-sidebar-border select-none">
      <div className="p-4 mb-2">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2.5 px-2 py-1.5 rounded-lg hover:bg-primary/5 transition-colors cursor-pointer group min-w-0">
            {brandMark ? (
              <img src={brandMark} alt={brandName} className="w-8 h-8 rounded-lg object-cover shrink-0 shadow-md shadow-primary/15 border border-border/40" />
            ) : (
              <div className="w-8 h-8 rounded-lg flex items-center justify-center shrink-0 shadow-md shadow-primary/15" style={{ background: "var(--primary)" }}>
                <span className="text-[11px] font-bold text-white">{getInitials(brandName)}</span>
              </div>
            )}
            <div className="overflow-hidden">
              <p className="font-[family-name:var(--font-display)] font-semibold text-[14px] tracking-tight truncate group-hover:text-primary transition-colors">{brandName}</p>
            </div>
          </div>
          {onClose && (
            <Button variant="ghost" size="sm" className="h-8 w-8 p-0" onClick={onClose} aria-label="Close sidebar">
              <X size={14} />
            </Button>
          )}
        </div>
      </div>

      <nav className="flex-1 overflow-y-auto px-3 space-y-3 custom-scrollbar">
        {visibleGroups.map((group, gi) => (
          <div key={group.section ?? `_root-${gi}`}>
            {group.section && (
              <p className="px-3 mb-1 text-[10.5px] font-bold uppercase tracking-[0.08em] text-muted-foreground/60">
                {group.section}
              </p>
            )}
            <div className="space-y-0.5">
              {group.items.map((item) => {
                const active = pathname === item.href || pathname.startsWith(item.href + "/");
                const i = itemIndex++;
                return (
                  <Link
                    key={item.href}
                    href={item.href}
                    onClick={onClose}
                    className={cn(
                      "flex items-center gap-2.5 px-3 py-2 rounded-[10px] text-[13px] font-medium transition-all duration-200 group relative",
                      active
                        ? "bg-primary/10 text-primary font-semibold"
                        : "text-muted-foreground hover:bg-black/5 dark:hover:bg-white/5 hover:text-foreground"
                    )}
                    style={{
                      animation: `fade-in-right 0.4s ease-out ${i * 30}ms both`,
                    }}
                  >
                    <item.icon size={16} className={cn(
                      "shrink-0 transition-colors duration-200",
                      active ? "text-primary" : "group-hover:text-primary"
                    )} />
                    {item.label}
                  </Link>
                );
              })}
            </div>
          </div>
        ))}
      </nav>

      <div className="p-4 mt-auto border-t border-sidebar-border bg-black/2 dark:bg-white/2">
        <div className="flex items-center gap-3 mb-4 px-2">
          <Avatar className="h-8 w-8 ring-2 ring-background shadow-md">
            <AvatarFallback name={user?.name} className="text-[10px]">{getInitials(user?.name ?? "M")}</AvatarFallback>
          </Avatar>
          <div className="overflow-hidden flex-1">
            <p className="text-[12px] font-bold truncate leading-tight">{user?.name}</p>
            <p className="text-[10px] text-muted-foreground/70 truncate">{user?.email}</p>
          </div>
        </div>
        <Link href="/settings" className="w-full block px-1" onClick={onClose}>
          <Button size="sm" variant="outline" className="h-8 w-full text-[11px] font-semibold border-border/50 hover:bg-background gap-1.5">
            <Settings size={12} />
            Settings
          </Button>
        </Link>
      </div>
    </div>
  );
}

function MobileBottomNav() {
  const pathname = usePathname();
  const disabledFeatures = useDisabledFeatures();

  const bottomNavItems = [
    { href: "/dashboard", label: "Home", icon: LayoutDashboard },
    { href: "/contributions", label: "Give", icon: CreditCard },
    { href: "/jobs", label: "Jobs", icon: Briefcase },
    { href: "/events", label: "Events", icon: Calendar },
    { href: "/profile", label: "Profile", icon: UserCircle },
  ].filter((item) => {
    const featureKey = NAV_FEATURE_KEYS[item.href];
    return !featureKey || !disabledFeatures.has(featureKey);
  });

  return (
    <div
      className="lg:hidden fixed bottom-0 left-0 right-0 z-40 border-t"
      style={{
        paddingBottom: "env(safe-area-inset-bottom)",
        background: "color-mix(in oklch, var(--background) 92%, transparent)",
        backdropFilter: "blur(20px)",
        borderColor: "var(--border)",
        ...GPU_LAYER_STYLE,
      }}
    >
      <nav className="flex items-stretch justify-around h-[58px] max-w-[560px] mx-auto">
        {bottomNavItems.map((item) => {
          const active = pathname === item.href || (item.href !== "/dashboard" && pathname.startsWith(item.href));
          return (
            <Link
              key={item.href}
              href={item.href}
              className="flex-1 flex flex-col items-center justify-center gap-0.5 relative"
              style={{ color: active ? "var(--primary)" : "var(--muted-foreground)" }}
            >
              <item.icon size={22} strokeWidth={active ? 2.4 : 1.9} />
              <span className={cn("text-[10.5px] leading-none", active ? "font-bold" : "font-medium")}>
                {item.label}
              </span>
              {active && (
                <div className="absolute top-0 w-8 h-[2.5px] rounded-full" style={{ background: "var(--primary)" }} />
              )}
            </Link>
          );
        })}
      </nav>
    </div>
  );
}

export function MemberLayout({ children }: { children: ReactNode }) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const { isLoading, isMember } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const { data: navTheme } = useNavTheme();
  const brandName = navTheme?.displayName || "Alumni Portal";
  const brandMark = navTheme?.iconUrl || navTheme?.logoUrl;

  useEffect(() => {
    if (!isLoading && !isMember && pathname !== "/login") {
      router.replace("/login");
    }
  }, [isLoading, isMember, pathname, router]);

  if (isLoading || !isMember) return null;

  return (
    <div className="flex h-dvh bg-background overflow-hidden overscroll-none selection:bg-primary/10">
      {/* Desktop sidebar */}
      <div className="hidden lg:flex shrink-0">
        <Sidebar />
      </div>

      {/* Mobile sidebar overlay (Drawer) */}
      {mobileOpen && (
        <div className="fixed inset-0 z-[9999] flex lg:hidden">
          <div className="fixed inset-0 bg-black/40 backdrop-blur-sm animate-in fade-in duration-300" style={GPU_LAYER_STYLE} onClick={() => setMobileOpen(false)} />
          <div className="relative z-10 animate-in slide-in-from-left duration-500 shadow-2xl">
            <Sidebar onClose={() => setMobileOpen(false)} />
          </div>
        </div>
      )}

      <div className="flex-1 flex flex-col min-w-0 bg-background relative">
        {/* Subtle top glow */}
        <div className="absolute top-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-primary/20 to-transparent" />
        
        {/* Desktop header */}
        <div
          className="hidden lg:flex items-center justify-end px-6 h-14 border-b border-border/40 bg-background/80 backdrop-blur-xl sticky top-0 z-40"
          style={GPU_LAYER_STYLE}
        >
          <NotificationPanel />
        </div>

        {/* Mobile header — `fixed`, not `sticky`: this div's actual ancestor is
            NOT a scrolling container (that's <main>, a sibling below it), so
            `sticky` never had a scroll box to stick within and was already
            behaving as a plain static element. On iOS Safari that's exactly
            what lets the whole page rubber-band/shift during a scroll gesture
            or address-bar collapse, visually dragging a "static" header out of
            view. `fixed` pins it to the true viewport regardless of any of
            that. transform/backfaceVisibility force it onto its own compositor
            layer, which also avoids a separate WebKit repaint bug where a
            fixed + backdrop-blur layer can fail to redraw after a sibling blur
            layer (a modal overlay, the notification panel) unmounts. */}
        <div
          className="lg:hidden fixed top-0 left-0 right-0 z-40 flex items-center justify-between px-4 sm:px-6 h-14 sm:h-16 border-b border-border/40 bg-background/80 backdrop-blur-xl"
          style={{ paddingTop: 'env(safe-area-inset-top)', ...GPU_LAYER_STYLE }}
        >
          <div className="flex items-center gap-2.5">
            {brandMark ? (
              <img src={brandMark} alt={brandName} className="w-8 h-8 rounded-lg object-cover shrink-0" />
            ) : (
              <div className="w-8 h-8 rounded-lg flex items-center justify-center shrink-0" style={{ background: "var(--primary)" }}>
                <span className="text-[12px] font-bold text-white">{getInitials(brandName)}</span>
              </div>
            )}
            <span className="font-bold text-[14.5px] leading-tight tracking-tight truncate max-w-[180px]">{brandName}</span>
          </div>
          <div className="flex items-center gap-1">
            <NotificationPanel />
            <Button 
              variant="ghost" 
              size="sm" 
              className="h-10 w-10 p-0 rounded-xl hover:bg-black/5 dark:hover:bg-white/5 transition-all active:scale-95" 
              onClick={() => setMobileOpen(true)}
              aria-label="Open full menu"
            >
              <Menu size={20} className="text-muted-foreground" />
            </Button>
          </div>
        </div>

        <main className="flex-1 overflow-y-auto overscroll-none bg-background selection:bg-primary/20 relative pt-14 sm:pt-16 lg:pt-0 pb-24 lg:pb-0 scroll-touch">
          <div className="w-full min-h-full max-w-[1800px] mx-auto px-0 sm:px-4 lg:px-8 py-0 sm:py-3 lg:py-6">
            <div className="w-full min-w-0">
              {children}
            </div>
          </div>
        </main>

        {/* Mobile Bottom Navigation */}
        <MobileBottomNav />
      </div>
    </div>
  );
}
