"use client";

import * as React from "react";
import { cn } from "../lib/utils";

const STACK_CLASSES = [
  "max-md:!min-w-0 max-md:block",
  "[&_thead]:max-md:hidden [&_tbody]:max-md:block",
  "[&_tr]:max-md:block [&_tr]:max-md:!h-auto [&_tr]:max-md:border [&_tr]:max-md:border-border/60 [&_tr]:max-md:mb-3 [&_tr]:max-md:p-3 [&_tbody_tr:last-child]:max-md:border",
  "[&_td]:max-md:flex [&_td]:max-md:items-center [&_td]:max-md:justify-between [&_td]:max-md:gap-4 [&_td]:max-md:!px-0 [&_td]:max-md:!py-1.5 [&_td]:max-md:text-right",
  "[&_td:first-child]:max-md:block [&_td:first-child]:max-md:text-left [&_td:first-child]:before:max-md:hidden",
  "[&_td]:before:max-md:content-[attr(data-label)] [&_td]:before:max-md:text-left [&_td]:before:max-md:text-[11px] [&_td]:before:max-md:font-bold [&_td]:before:max-md:uppercase [&_td]:before:max-md:tracking-[.08em] [&_td]:before:max-md:text-muted-foreground",
].join(" ");

/** Copies each column header onto its cells so the stacked phone layout can show "Label  value" rows. */
function labelCells(table: HTMLTableElement) {
  const headers = Array.from(table.querySelectorAll("thead th")).map((th) => (th.textContent ?? "").trim());
  table.querySelectorAll("tbody tr").forEach((tr) => {
    Array.from(tr.children).forEach((cell, i) => {
      const label = (cell as HTMLTableCellElement).colSpan > 1 ? "" : headers[i] ?? "";
      if (cell.getAttribute("data-label") !== label) cell.setAttribute("data-label", label);
    });
  });
}

interface TableProps extends React.HTMLAttributes<HTMLTableElement> {
  /** Below `md`, show each row as a small card of "Label  value" lines instead of a sideways-scrolling table. */
  stackOnMobile?: boolean;
}

const Table = React.forwardRef<HTMLTableElement, TableProps>(
  ({ className, stackOnMobile, ...props }, ref) => {
    const inner = React.useRef<HTMLTableElement | null>(null);

    React.useEffect(() => {
      const table = inner.current;
      if (!stackOnMobile || !table) return;
      labelCells(table);
      const observer = new MutationObserver(() => labelCells(table));
      observer.observe(table, { childList: true, subtree: true });
      return () => observer.disconnect();
    }, [stackOnMobile]);

    return (
      <div className="relative w-full overflow-x-auto overscroll-x-contain [scrollbar-gutter:stable] [scrollbar-width:thin]">
        <table
          ref={(node) => {
            inner.current = node;
            if (typeof ref === "function") ref(node);
            else if (ref) ref.current = node;
          }}
          className={cn("w-full min-w-[720px] border-collapse text-[14px]", stackOnMobile && STACK_CLASSES, className)}
          {...props}
        />
      </div>
    );
  }
);
Table.displayName = "Table";

const TableHeader = React.forwardRef<HTMLTableSectionElement, React.HTMLAttributes<HTMLTableSectionElement>>(
  ({ className, ...props }, ref) => (
    <thead ref={ref} className={cn("bg-muted/35 [&_tr]:border-b [&_tr]:border-border/60", className)} {...props} />
  )
);
TableHeader.displayName = "TableHeader";

const TableBody = React.forwardRef<HTMLTableSectionElement, React.HTMLAttributes<HTMLTableSectionElement>>(
  ({ className, ...props }, ref) => (
    <tbody ref={ref} className={cn("[&_tr:last-child]:border-0", className)} {...props} />
  )
);
TableBody.displayName = "TableBody";

const TableRow = React.forwardRef<HTMLTableRowElement, React.HTMLAttributes<HTMLTableRowElement>>(
  ({ className, ...props }, ref) => (
    <tr
      ref={ref}
      className={cn(
        "h-[52px] border-b border-border/45 transition-colors duration-100 ease-out",
        "hover:bg-muted/30 data-[state=selected]:bg-accent/5",
        className
      )}
      {...props}
    />
  )
);
TableRow.displayName = "TableRow";

const TableHead = React.forwardRef<HTMLTableCellElement, React.ThHTMLAttributes<HTMLTableCellElement>>(
  ({ className, ...props }, ref) => (
    <th
      ref={ref}
      className={cn(
        "h-11 px-4 py-3 text-left align-middle",
        "text-[11px] font-bold uppercase tracking-[.08em] text-muted-foreground",
        "[&:has([role=checkbox])]:pr-0",
        className
      )}
      {...props}
    />
  )
);
TableHead.displayName = "TableHead";

const TableCell = React.forwardRef<HTMLTableCellElement, React.TdHTMLAttributes<HTMLTableCellElement>>(
  ({ className, ...props }, ref) => (
    <td
      ref={ref}
      className={cn("px-4 py-3 align-middle text-[14px] text-foreground [&:has([role=checkbox])]:pr-0", className)}
      {...props}
    />
  )
);
TableCell.displayName = "TableCell";

function TableEmpty({ icon, title, description, action, colSpan = 99 }: {
  icon?: React.ReactNode;
  title: string;
  description?: string;
  action?: React.ReactNode;
  colSpan?: number;
}) {
  return (
    <tr>
      <td colSpan={colSpan} className="py-16 text-center">
        <div className="flex flex-col items-center gap-3">
          {icon && <div className="text-muted-foreground/40">{icon}</div>}
          <p className="text-[14px] font-medium text-muted-foreground">{title}</p>
          {description && <p className="text-[13px] text-muted-foreground/70 max-w-[280px]">{description}</p>}
          {action && <div className="mt-2">{action}</div>}
        </div>
      </td>
    </tr>
  );
}

export { Table, TableHeader, TableBody, TableRow, TableHead, TableCell, TableEmpty };
