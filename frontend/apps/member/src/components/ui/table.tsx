import * as React from "react";
import { cn } from "@/lib/utils";

const Table = React.forwardRef<HTMLTableElement, React.HTMLAttributes<HTMLTableElement>>(
  ({ className, ...props }, ref) => (
    <div className="relative w-full overflow-x-auto overscroll-x-contain [scrollbar-gutter:stable] [scrollbar-width:thin]">
      <table ref={ref} className={cn("w-full min-w-[720px] border-collapse text-[14px]", className)} {...props} />
    </div>
  )
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
        "hover:bg-muted/30 data-[state=selected]:bg-primary/5",
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
        "text-[11px] font-semibold uppercase tracking-[0.06em] text-muted-foreground",
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
