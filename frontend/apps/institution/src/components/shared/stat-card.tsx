import { cn } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
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
    <Card className="border-border bg-card overflow-hidden min-w-0">
      <CardContent className="p-5">
        <div className="flex items-start justify-between min-w-0">
          <div className="space-y-1 min-w-0">
            <p className="text-[12px] font-semibold text-muted-foreground truncate">{title}</p>
            <p
              className={cn(valueSizeClass, "font-bold tracking-tight text-foreground break-all max-w-[12rem]")}
              title={valueStr}
            >
              {valueStr}
            </p>
            {subtitle && <p className="text-[12px] text-muted-foreground truncate">{subtitle}</p>}
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
              "w-9 h-9 rounded-md flex items-center justify-center shrink-0",
              iconClassName ?? "bg-primary/10 text-primary"
            )}
          >
            <Icon size={18} className={cn(!iconClassName && "text-primary")} />
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
