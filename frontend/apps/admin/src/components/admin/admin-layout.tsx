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
  X,
} from "lucide-react";
import { useState, useEffect, useMemo } from "react";
import Image from "next/image";
import { cn } from "@/lib/utils";
import { useAuth } from "@/hooks/use-auth";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { getInitials } from "@/lib/utils";

const baseNavItems = [
  { href: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
  { href: "/members", label: "Members", icon: Users },
  { href: "/campaigns", label: "Campaigns", icon: Megaphone },
  { href: "/membership", label: "Membership Renewal", icon: CreditCard },
  { href: "/contributions", label: "Contributions", icon: CreditCard },
  { label: "Community", isHeader: true },
  { href: "/jobs", label: "Job Board", icon: Briefcase },
  { href: "/events", label: "Events", icon: Calendar },
  { href: "/news", label: "News", icon: Newspaper },
  { href: "/forum", label: "Forum", icon: MessageSquare },
  { href: "/mentorship", label: "Mentorship", icon: GraduationCap },
  { href: "/resources", label: "Resources", icon: FolderOpen },
  { href: "/spotlights", label: "Spotlights", icon: Star },
  { label: "Reports", isHeader: true },
  { href: "/reports", label: "Reports & Exports", icon: BarChart3 },
];

export function AdminSidebar({ onClose }: { onClose?: () => void }) {
  const pathname = usePathname();
  const { user, logout } = useAuth();
  const navItems = useMemo(() => {
    const items = baseNavItems.filter((item) => {
      if (item.href === "/forum" || item.href === "/mentorship") {
        return user?.role === "SuperAdmin";
      }
      return true;
    });

    if (user?.role === "SuperAdmin") {
      items.splice(2, 0, { href: "/admins", label: "Admins", icon: ShieldCheck });
    }
    return items;
  }, [user?.role]);

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
              <p className="text-[10px] text-muted-foreground/60 font-medium tracking-wide uppercase">Admin Portal</p>
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
          if (item.isHeader) {
            return (
              <p key={i} className="px-3 py-2 text-[10px] font-bold text-muted-foreground/40 uppercase tracking-[0.12em] mt-6 mb-1 first:mt-0">
                {item.label}
              </p>
            );
          }

          const active = pathname === item.href || pathname.startsWith(item.href + "/");
          return (
            <Link
              key={item.href}
              href={item.href!}
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
              {item.icon && <item.icon size={16} className={cn(
                "shrink-0 transition-colors duration-200",
                active ? "text-primary" : "group-hover:text-primary"
              )} />}
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
            <AvatarFallback name={user?.name} className="text-[10px]">{getInitials(user?.name ?? "A")}</AvatarFallback>
          </Avatar>
          <div className="overflow-hidden flex-1">
            <p className="text-[12px] font-bold truncate leading-tight uppercase tracking-tight">{user?.name ?? "Administrator"}</p>
            <p className="text-[10px] text-primary font-bold uppercase tracking-wider">{user?.role ?? "Super Admin"}</p>
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

export function AdminLayout({ children }: { children: React.ReactNode }) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const { isLoading, isAdmin } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

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
        {/* Subtle top glow */}
        <div className="absolute top-0 left-0 right-0 h-px bg-gradient-to-r from-transparent via-primary/20 to-transparent shadow-[0_1px_10px_rgba(var(--primary-rgb),0.1)]" />
        
        {/* Mobile header */}
        <div className="lg:hidden sticky top-0 z-10 flex items-center justify-between px-4 h-14 border-b border-border/40 bg-background/80 backdrop-blur-xl">
          <div className="flex items-center gap-3">
            <Button variant="ghost" size="sm" className="h-9 w-9 p-0 rounded-lg hover:bg-muted" onClick={() => setMobileOpen(true)} aria-label="Open menu">
              <Menu size={20} />
            </Button>
            <span className="font-bold text-[15px] tracking-tight">UMaT Admin</span>
          </div>
          <div className="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center border border-primary/20">
            <span className="text-[10px] font-bold text-primary">AD</span>
          </div>
        </div>

        <main className="flex-1 overflow-y-auto bg-background selection:bg-primary/20 relative">
          <div className="max-w-[1800px] mx-auto min-h-full">
            {children}
          </div>
        </main>
      </div>
    </div>
  );
}
