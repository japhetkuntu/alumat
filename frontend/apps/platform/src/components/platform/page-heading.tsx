import type { ReactNode } from "react";

/**
 * Page title, description and page-level actions. On a phone the actions drop below the text and share the
 * row width (so none run off the edge); from `sm` up they sit to the right of the title.
 */
export function PageHeading({ title, description, children }: { title: string; description?: string; children?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between sm:gap-6">
      <div className="min-w-0 max-w-2xl">
        <h1 className="text-[22px] font-bold leading-tight tracking-tight sm:text-[26px]">{title}</h1>
        {description && <p className="mt-1.5 text-[14px] leading-relaxed text-muted-foreground sm:text-[13px]">{description}</p>}
      </div>
      {children && (
        <div className="flex flex-wrap items-center gap-2 sm:shrink-0 sm:justify-end max-sm:[&>*]:min-w-[calc(50%-0.25rem)] max-sm:[&>*]:grow max-sm:[&_a_button]:w-full">
          {children}
        </div>
      )}
    </div>
  );
}
