"use client";

import Link from "next/link";
import Image from "next/image";
import { usePathname, useRouter } from "next/navigation";
import { useState, useEffect, ReactNode } from "react";
import { useAuth } from "@/hooks/use-auth";
import { cn, getInitials } from "@/lib/utils";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { NotificationPanel } from "@/components/member/notification-panel";
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
  Bell,
} from "lucide-react";

const navItems = [
  { href: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
  { href: "/contributions", label: "My Contributions", icon: CreditCard },
  { href: "/jobs", label: "Job Board", icon: Briefcase },
  { href: "/events", label: "Events", icon: Calendar },
  { href: "/directory", label: "Alumni Directory", icon: Users },
  { href: "/news", label: "News", icon: Newspaper },
  { href: "/forum", label: "Forum", icon: MessageSquare },
  { href: "/mentorship", label: "Mentorship", icon: GraduationCap },
  { href: "/resources", label: "Resources", icon: FolderOpen },
  { href: "/leaderboard", label: "Leaderboard", icon: Trophy },
  { href: "/spotlights", label: "Spotlights", icon: Star },
  { href: "/referrals", label: "Refer a Friend", icon: UserPlus },
  { href: "/class-notes", label: "Class Notes", icon: StickyNote },
  { href: "/notifications", label: "Notifications", icon: Bell },
  { href: "/profile", label: "My Profile", icon: UserCircle },
];

function Sidebar({ onClose }: { onClose?: () => void }) {
  const pathname = usePathname();
  const { user, logout } = useAuth();

  return (
    <div className="flex flex-col h-full bg-gradient-to-b from-emerald-50/60 to-slate-50 dark:from-[#0d1411] dark:to-[#111] w-[240px] border-r border-border/40 select-none">
      <div className="p-4 mb-2">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2.5 px-2 py-1.5 rounded-lg hover:bg-primary/5 dark:hover:bg-white/5 transition-colors cursor-pointer group">
            <div className="w-8 h-8 rounded-lg overflow-hidden shrink-0 shadow-lg shadow-emerald-500/25 border border-white/20 bg-white/10">
              <Image src="/umat-logo.svg" alt="UMaT Logo" width={32} height={32} className="object-contain" />
            </div>
            <div className="overflow-hidden">
              <p className="font-bold text-[13px] tracking-tight truncate group-hover:text-primary transition-colors">UMaT Alumni</p>
              <p className="text-[10px] text-muted-foreground/60 font-medium tracking-wide uppercase">Member Portal</p>
            </div>
          </div>
          {onClose && (
            <Button variant="ghost" size="sm" className="h-8 w-8 p-0" onClick={onClose} aria-label="Close sidebar">
              <X size={14} />
            </Button>
          )}
        </div>
      </div>

      <nav className="flex-1 overflow-y-auto px-3 space-y-0.5 custom-scrollbar">
        {navItems.map((item, i) => {
          const active = pathname === item.href || pathname.startsWith(item.href + "/");
          return (
            <Link
              key={item.href}
              href={item.href}
              onClick={onClose}
              className={cn(
                "flex items-center gap-2.5 px-3 py-1.5 rounded-lg text-[13px] font-medium transition-all duration-200 group relative",
                active
                  ? "bg-white dark:bg-white/10 text-foreground shadow-sm border border-border/40"
                  : "text-muted-foreground hover:bg-black/5 dark:hover:bg-white/5 hover:text-foreground"
              )}
              style={{ 
                animationDelay: `${i * 30}ms`,
                animation: 'fade-in-right 0.4s ease-out both'
              }}
            >
              <item.icon size={16} className={cn(
                "shrink-0 transition-colors duration-200",
                active ? "text-primary" : "group-hover:text-primary"
              )} />
              {item.label}
              {active && (
                <div className="absolute left-0 w-1 h-4 bg-primary rounded-r-full" />
              )}
            </Link>
          );
        })}
      </nav>

      <div className="p-4 mt-auto border-t border-border/30 bg-black/2 dark:bg-white/2">
        <div className="flex items-center gap-3 mb-4 px-2">
          <Avatar className="h-8 w-8 ring-2 ring-background shadow-md">
            <AvatarFallback name={user?.name} className="text-[10px]">{getInitials(user?.name ?? "M")}</AvatarFallback>
          </Avatar>
          <div className="overflow-hidden flex-1">
            <p className="text-[12px] font-bold truncate leading-tight">{user?.name}</p>
            <p className="text-[10px] text-muted-foreground/70 truncate">{user?.email}</p>
          </div>
        </div>
        <div className="grid grid-cols-2 gap-2 px-1">
          <Link href="/settings" className="w-full" onClick={onClose}>
            <Button size="sm" variant="outline" className="h-8 w-full text-[11px] font-semibold border-border/50 hover:bg-background gap-1.5">
              <Settings size={12} />
              Settings
            </Button>
          </Link>
          <Button size="sm" variant="ghost" className="h-8 text-[11px] font-semibold text-destructive hover:bg-destructive/10 hover:text-destructive" onClick={logout}>
            Log out
          </Button>
        </div>
      </div>
    </div>
  );
}

function MobileBottomNav() {
  const pathname = usePathname();
  
  const bottomNavItems = [
    { href: "/dashboard", label: "Home", icon: LayoutDashboard },
    { href: "/contributions", label: "Pay", icon: CreditCard },
    { href: "/jobs", label: "Jobs", icon: Briefcase },
    { href: "/events", label: "Events", icon: Calendar },
    { href: "/profile", label: "Profile", icon: UserCircle },
  ];

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
              <Image src="/umat-logo.svg" alt="UMaT Logo" width={32} height={32} className="object-contain" />
            </div>
            <div className="flex flex-col">
              <span className="font-black text-[14px] leading-tight tracking-tight">UMaT Alumni</span>
              <span className="text-[9px] font-bold text-muted-foreground/60 uppercase tracking-widest">Member Portal</span>
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
