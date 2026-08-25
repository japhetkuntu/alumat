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
} from "lucide-react";

// Grouped by what a member is trying to DO, not by feature type — keeps the
// sidebar scannable and gives "Home" a clear job (the daily-return feed)
// instead of competing for attention with 15 flat siblings.
const navGroups: { section: string | null; items: { href: string; label: string; icon: typeof LayoutDashboard }[] }[] = [
  {
    section: null,
    items: [
      { href: "/dashboard", label: "Home", icon: LayoutDashboard },
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
      { href: "/spotlights", label: "Spotlights", icon: Star },
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
  "/alumni-map": "Directory",
  "/news": "News",
  "/forum": "Forum",
  "/mentorship": "Mentorship",
  "/resources": "Resources",
  "/leaderboard": "Leaderboard",
  "/spotlights": "Spotlights",
  "/referrals": "Referrals",
  "/class-notes": "ClassNotes",
};

/** Shared across Sidebar and MobileBottomNav — react-query dedupes the fetch since both use the same queryKey. */
function useDisabledFeatures(): Set<string> {
  const { data } = useQuery({
    queryKey: ["member-nav-theme"],
    queryFn: async () => {
      const res = await memberClient.get<{ data: { disabledFeatures: string[] } }>("/public/institution/theme");
      return res.data.data;
    },
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
  return useMemo(() => new Set(data?.disabledFeatures ?? []), [data]);
}

function Sidebar({ onClose }: { onClose?: () => void }) {
  const pathname = usePathname();
  const { user } = useAuth();
  const disabledFeatures = useDisabledFeatures();
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
          <div className="flex items-center gap-2.5 px-2 py-1.5 rounded-lg hover:bg-primary/5 transition-colors cursor-pointer group">
            <div className="w-8 h-8 rounded-lg overflow-hidden shrink-0 shadow-md shadow-primary/15 border border-border/40 bg-primary/5">
              <img src="/logo.svg" alt="Logo" width={32} height={32} className="object-contain" />
            </div>
            <div className="overflow-hidden">
              <p className="font-[family-name:var(--font-display)] font-semibold text-[14px] tracking-tight truncate group-hover:text-primary transition-colors">Alumni Portal</p>
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
    <div className="lg:hidden fixed bottom-0 left-0 right-0 z-40 px-3 sm:px-4" style={{ paddingBottom: 'max(0.75rem, env(safe-area-inset-bottom))' }}>
      <nav className="bg-white/70 dark:bg-black/70 backdrop-blur-2xl border border-white/20 dark:border-white/5 shadow-[0_-8px_30px_rgb(0,0,0,0.12)] rounded-[2rem] flex items-center justify-around h-[60px] sm:h-16 px-1 sm:px-2">
        {bottomNavItems.map((item) => {
          const active = pathname === item.href || (item.href !== "/dashboard" && pathname.startsWith(item.href));
          return (
            <Link 
              key={item.href} 
              href={item.href}
              className={cn(
                "flex flex-col items-center justify-center gap-1 min-w-[64px] transition-all duration-300 relative",
                active ? "text-primary px-2" : "text-muted-foreground/60 hover:text-foreground"
              )}
            >
              <div className={cn(
                "p-1.5 rounded-xl transition-all duration-500",
                active ? "bg-primary/10 scale-110" : "group-hover:bg-muted"
              )}>
                <item.icon size={20} className={cn("transition-transform duration-300", active && "scale-110")} />
              </div>
              <span className={cn(
                "text-[10px] font-bold tracking-tight transition-all duration-300",
                active ? "opacity-100 scale-100" : "opacity-0 scale-90 h-0 overflow-hidden"
              )}>
                {item.label}
              </span>
              {active && (
                <div className="absolute -top-1 w-1 h-1 bg-primary rounded-full animate-in zoom-in duration-300" />
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

  useEffect(() => {
    if (!isLoading && !isMember && pathname !== "/login") {
      router.replace("/login");
    }
  }, [isLoading, isMember, pathname, router]);

  if (isLoading || !isMember) return null;

  return (
    <div className="flex h-screen bg-background overflow-hidden selection:bg-primary/10">
      {/* Desktop sidebar */}
      <div className="hidden lg:flex shrink-0">
        <Sidebar />
      </div>

      {/* Mobile sidebar overlay (Drawer) */}
      {mobileOpen && (
        <div className="fixed inset-0 z-[9999] flex lg:hidden">
          <div className="fixed inset-0 bg-black/40 backdrop-blur-sm animate-in fade-in duration-300" onClick={() => setMobileOpen(false)} />
          <div className="relative z-10 animate-in slide-in-from-left duration-500 shadow-2xl">
            <Sidebar onClose={() => setMobileOpen(false)} />
          </div>
        </div>
      )}

      <div className="flex-1 flex flex-col min-w-0 bg-background relative">
        {/* Subtle top glow */}
        <div className="absolute top-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-primary/20 to-transparent" />
        
        {/* Desktop header */}
        <div className="hidden lg:flex items-center justify-end px-6 h-14 border-b border-border/40 bg-background/80 backdrop-blur-xl sticky top-0 z-40">
          <NotificationPanel />
        </div>

        {/* Mobile header - Refined and compact */}
        <div className="lg:hidden sticky top-0 z-40 flex items-center justify-between px-4 sm:px-6 h-14 sm:h-16 border-b border-border/40 bg-background/80 backdrop-blur-xl" style={{ paddingTop: 'env(safe-area-inset-top)' }}>
          <div className="flex items-center gap-3">
            <div className="w-8 h-8 rounded-lg overflow-hidden shrink-0 shadow-lg shadow-primary/20 border border-white/20 bg-white/10">
              <img src="/logo.svg" alt="Logo" width={32} height={32} className="object-contain" />
            </div>
            <div className="flex flex-col">
              <span className="font-black text-[14px] leading-tight tracking-tight">Alumni Portal</span>
              {/* <span className="text-[9px] font-bold text-muted-foreground/60 uppercase tracking-widest">Member Portal</span> */}
            </div>
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

        <main className="flex-1 overflow-y-auto bg-background selection:bg-primary/20 relative pb-28 sm:pb-32 lg:pb-0 scroll-touch">
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
