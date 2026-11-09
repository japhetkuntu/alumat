"use client";

import { useQueries } from "@tanstack/react-query";

/**
 * How many items each filter chip would show, so a chip can say "Pending · 3" and read as something worth opening.
 * One small request per chip, each asking a list endpoint for a single row and reading its total.
 *
 * `keyPrefix` is the list's own query key prefix (e.g. ["admin-jobs"]): the counts are stored under it, so everything that refreshes the
 * list after a create, edit or delete refreshes the counts as well, with nothing extra to wire up.
 *
 * Counts ignore any other filter or search the page has on, as a count of "everything of this kind" is what the chip promises.
 * Returns undefined for a count still loading or that failed, and the chip simply shows no number.
 */
export function useFilterCounts<T extends string>(
  keyPrefix: readonly unknown[],
  filters: readonly T[],
  count: (filter: T) => Promise<number | undefined>,
  /** `scope` is anything else the counts depend on (a group chosen elsewhere on the page), so a change of scope counts afresh. */
  options: { enabled?: boolean; scope?: unknown } = {},
): Record<string, number | undefined> {
  const results = useQueries({
    queries: filters.map((f) => ({
      queryKey: [...keyPrefix, "count", options.scope ?? null, f] as const,
      queryFn: async () => (await count(f)) ?? null,
      staleTime: 60_000,
      retry: false,
      enabled: options.enabled ?? true,
    })),
  });
  const out: Record<string, number | undefined> = {};
  filters.forEach((f, i) => { out[f] = results[i]?.data ?? undefined; });
  return out;
}

/** A chip's label with its count: "Pending · 3". No number, no dot, while the count is unknown. */
export const withCount = (label: string, count: number | undefined | null) =>
  count == null ? label : `${label} · ${count.toLocaleString()}`;
