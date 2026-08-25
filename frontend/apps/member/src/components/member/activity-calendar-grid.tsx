"use client";

import { useMemo, useState } from "react";
import {
  addMonths,
  eachDayOfInterval,
  endOfMonth,
  endOfWeek,
  format,
  isSameDay,
  isSameMonth,
  isToday as isTodayFn,
  startOfMonth,
  startOfWeek,
  subMonths,
} from "date-fns";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { cn } from "@alumni/ui";

export type CalendarItem = {
  id: string;
  type: "event" | "campaign";
  title: string;
  date: string;
  href: string;
  meta: string;
  mine: boolean;
  done: boolean;
  badge: string;
};

const WEEKDAY_LABELS = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

function dotColor(item: CalendarItem, temporal: "past" | "today" | "future") {
  if (temporal === "past") return "var(--muted-foreground)";
  return item.type === "event" ? "var(--info)" : "var(--warning)";
}

export function ActivityCalendarGrid({
  itemsByDate,
  selectedDate,
  onSelectDate,
}: {
  itemsByDate: Map<string, CalendarItem[]>;
  selectedDate: Date | null;
  onSelectDate: (date: Date) => void;
}) {
  const [visibleMonth, setVisibleMonth] = useState(() => startOfMonth(new Date()));

  const days = useMemo(() => {
    const start = startOfWeek(startOfMonth(visibleMonth));
    const end = endOfWeek(endOfMonth(visibleMonth));
    return eachDayOfInterval({ start, end });
  }, [visibleMonth]);

  const today = useMemo(() => new Date(), []);

  function goToday() {
    setVisibleMonth(startOfMonth(new Date()));
    onSelectDate(new Date());
  }

  return (
    <div className="w-full">
      <div className="flex items-center justify-between gap-2 mb-3 sm:mb-4">
        <p className="text-[15px] sm:text-[17px] font-bold truncate" style={{ color: "var(--foreground)" }}>
          {format(visibleMonth, "MMMM yyyy")}
        </p>
        <div className="flex items-center gap-1.5 shrink-0">
          <button
            type="button"
            onClick={goToday}
            className="px-2.5 py-1.5 rounded-lg text-[12px] font-semibold transition-colors hover:bg-muted"
            style={{ color: "var(--muted-foreground)" }}
          >
            Today
          </button>
          <button
            type="button"
            aria-label="Previous month"
            onClick={() => setVisibleMonth((m) => subMonths(m, 1))}
            className="w-9 h-9 rounded-lg flex items-center justify-center transition-colors hover:bg-muted"
            style={{ color: "var(--foreground)" }}
          >
            <ChevronLeft size={16} />
          </button>
          <button
            type="button"
            aria-label="Next month"
            onClick={() => setVisibleMonth((m) => addMonths(m, 1))}
            className="w-9 h-9 rounded-lg flex items-center justify-center transition-colors hover:bg-muted"
            style={{ color: "var(--foreground)" }}
          >
            <ChevronRight size={16} />
          </button>
        </div>
      </div>

      <div className="grid grid-cols-7 gap-1 mb-1.5">
        {WEEKDAY_LABELS.map((d) => (
          <div
            key={d}
            className="text-center text-[10px] sm:text-[11px] font-bold uppercase tracking-wide py-1"
            style={{ color: "var(--muted-foreground)" }}
          >
            {d}
          </div>
        ))}
      </div>

      <div className="grid grid-cols-7 gap-1">
        {days.map((day) => {
          const key = format(day, "yyyy-MM-dd");
          const inMonth = isSameMonth(day, visibleMonth);
          const dayIsToday = isTodayFn(day);
          const isSelected = !!selectedDate && isSameDay(day, selectedDate);
          const items = inMonth ? itemsByDate.get(key) ?? [] : [];
          const temporal: "past" | "today" | "future" = dayIsToday
            ? "today"
            : day < today
              ? "past"
              : "future";
          const visibleDots = items.slice(0, 3);
          const overflow = items.length - visibleDots.length;

          return (
            <button
              key={key}
              type="button"
              onClick={() => onSelectDate(day)}
              aria-pressed={isSelected}
              aria-label={`${format(day, "EEEE, MMMM d, yyyy")}${items.length ? `, ${items.length} ${items.length === 1 ? "activity" : "activities"}` : ""}`}
              className={cn(
                "relative flex flex-col items-center justify-start rounded-lg sm:rounded-xl p-1 sm:p-1.5",
                "min-h-[52px] sm:min-h-[68px] lg:min-h-[92px]",
                "transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-ring/50",
                inMonth ? "hover:bg-muted/60" : "opacity-40 hover:bg-muted/30",
                isSelected && "ring-2 ring-[var(--ring)]"
              )}
            >
              <span
                className={cn(
                  "flex items-center justify-center w-6 h-6 sm:w-7 sm:h-7 rounded-full text-[12px] sm:text-[13px] font-semibold shrink-0",
                  dayIsToday && "font-bold"
                )}
                style={{
                  background: dayIsToday ? "var(--primary)" : "transparent",
                  color: dayIsToday ? "var(--primary-foreground)" : "var(--foreground)",
                }}
              >
                {format(day, "d")}
              </span>

              {/* Mobile / small: dot indicators only */}
              {items.length > 0 && (
                <div className="flex items-center justify-center gap-[3px] mt-1 sm:hidden">
                  {visibleDots.map((item) => (
                    <span
                      key={item.id}
                      className="w-1.5 h-1.5 rounded-full shrink-0"
                      style={{ background: dotColor(item, temporal) }}
                    />
                  ))}
                  {overflow > 0 && (
                    <span className="text-[9px] font-bold leading-none" style={{ color: "var(--muted-foreground)" }}>
                      +{overflow}
                    </span>
                  )}
                </div>
              )}

              {/* Tablet / desktop: title chips */}
              {items.length > 0 && (
                <div className="hidden sm:flex flex-col gap-0.5 w-full mt-1 min-w-0">
                  {items.slice(0, 2).map((item) => (
                    <span
                      key={item.id}
                      className="w-full truncate text-left text-[9.5px] lg:text-[10.5px] font-medium rounded px-1 py-[1px] leading-tight"
                      style={{
                        background: `color-mix(in srgb, ${dotColor(item, temporal)} 14%, transparent)`,
                        color: temporal === "past" ? "var(--muted-foreground)" : "var(--foreground)",
                      }}
                    >
                      {item.title}
                    </span>
                  ))}
                  {items.length > 2 && (
                    <span className="text-[9px] font-semibold px-1" style={{ color: "var(--muted-foreground)" }}>
                      +{items.length - 2} more
                    </span>
                  )}
                </div>
              )}
            </button>
          );
        })}
      </div>

      <div className="flex flex-wrap items-center gap-x-4 gap-y-1.5 mt-4 pt-3 border-t" style={{ borderColor: "var(--border)" }}>
        <LegendDot color="var(--info)" label="Event" />
        <LegendDot color="var(--warning)" label="Payment due" />
        <LegendDot color="var(--muted-foreground)" label="Past" />
      </div>
    </div>
  );
}

function LegendDot({ color, label }: { color: string; label: string }) {
  return (
    <div className="flex items-center gap-1.5">
      <span className="w-2 h-2 rounded-full shrink-0" style={{ background: color }} />
      <span className="text-[11px] font-medium" style={{ color: "var(--muted-foreground)" }}>
        {label}
      </span>
    </div>
  );
}
