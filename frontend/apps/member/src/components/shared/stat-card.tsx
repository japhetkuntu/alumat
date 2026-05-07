import { cn } from "@/lib/utils";
import { Card, CardContent } from "@/components/ui/card";
import { LucideIcon } from "lucide-react";

interface StatCardProps {
  title: string;
  value: string | number;
  subtitle?: string;
  icon: LucideIcon;
  iconClassName?: string;
  trend?: { value: number; label: string };
}

export function StatCard({ title, value, subtitle, icon: Icon, iconClassName, trend }: StatCardProps) {
  const valueStr = String(value);
  const valueSizeClass = valueStr.length > 14 ? "text-xl" : valueStr.length > 12 ? "text-2xl" : valueStr.length > 9 ? "text-3xl" : "text-4xl";

  return (
    <Card className="group hover:shadow-lg transition-all duration-300 border-border/40 hover:border-primary/20 bg-card overflow-hidden relative min-w-0">
      <CardContent className="p-6 relative z-10">
        <div className="flex items-start justify-between min-w-0">
          <div className="space-y-1 min-w-0">
            <p className="text-[12px] font-bold text-muted-foreground/70 uppercase tracking-widest truncate">{title}</p>
            <p
              className={cn(
                valueSizeClass,
                "font-extrabold tracking-tight text-foreground transition-all duration-300 group-hover:text-primary break-all max-w-[12rem]"
              )}
              title={valueStr}
            >
              {valueStr}
            </p>
            {subtitle && <p className="text-[12px] text-muted-foreground font-medium truncate">{subtitle}</p>}
            {trend && (
              <div
                className={cn(
                  "inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-bold mt-2",
                  trend.value >= 0
                    ? "bg-success/10 text-success"
                    : "bg-destructive/10 text-destructive"
                )}
              >
                {trend.value >= 0 ? "↑" : "↓"} {Math.abs(trend.value)}% {trend.label}
              </div>
            )}
          </div>
          <div
            className={cn(
              "w-12 h-12 rounded-2xl flex items-center justify-center transition-all duration-500 group-hover:rotate-[10deg] shadow-lg shadow-black/5",
              iconClassName ?? "bg-primary/10 text-primary"
            )}
          >
            <Icon size={22} className={cn(!iconClassName && "text-primary")} />
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
