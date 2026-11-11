"use client";

import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Badge, Button, Card, CheckOption, EmptyState, FormSheet, Globe, Input, LoadError, Search, Textarea, cn, formatCurrency, formatDate } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { handleApiError } from "@/lib/api-client";
import { getShowcase, saveShowcase, type ShowcaseEntry, type ShowcaseStat } from "@/lib/platform-api";
import { PageHeading } from "@/components/platform/page-heading";

const BLURB_MAX = 160;

const STATS: { key: ShowcaseStat; label: string; hint: string }[] = [
  { key: "members", label: "Active members", hint: "People with an active account." },
  { key: "groups", label: "Groups and year groups", hint: "Active groups and year groups together." },
  { key: "events", label: "Events held", hint: "Events that have already taken place and were not cancelled." },
  { key: "raised", label: "Money raised", hint: "Everything given through the institution's fundraisers. Only show it if the institution is comfortable with that." },
];

const figure = (key: ShowcaseStat, n: ShowcaseEntry["numbers"]) => key === "raised" ? formatCurrency(n.raised, "GHS") : n[key].toLocaleString();

function Logo({ entry, size = 40 }: { entry: Pick<ShowcaseEntry, "name" | "logoUrl">; size?: number }) {
  return entry.logoUrl
    // eslint-disable-next-line @next/next/no-img-element
    ? <img src={entry.logoUrl} alt="" width={size} height={size} className="shrink-0 rounded-md bg-white object-contain ring-1 ring-border" style={{ width: size, height: size }} />
    : <span aria-hidden className="grid shrink-0 place-items-center rounded-md bg-muted text-[13px] font-semibold text-muted-foreground" style={{ width: size, height: size }}>{entry.name.slice(0, 2).toUpperCase()}</span>;
}

/** What the website will say, as a visitor reads it, so staff see the result of their choices before saving. */
function Preview({ entry, blurb, stats }: { entry: ShowcaseEntry; blurb: string; stats: ShowcaseStat[] }) {
  const shown = STATS.filter((s) => stats.includes(s.key)).map((s) => ({ ...s, value: figure(s.key, entry.numbers) })).filter((s) => entry.numbers[s.key] > 0);
  return (
    <div className="border border-border bg-card p-4">
      <div className="flex items-center gap-3">
        <Logo entry={entry} size={44} />
        <p className="min-w-0 truncate text-[15px] font-semibold">{entry.name}</p>
      </div>
      {blurb.trim() && <p className="mt-3 text-[13.5px] leading-relaxed text-muted-foreground">{blurb.trim()}</p>}
      {shown.length > 0 && (
        <dl className="mt-3 flex flex-wrap gap-x-6 gap-y-2 border-t border-border pt-3">
          {shown.map((s) => (
            <div key={s.key}>
              <dd className="text-[17px] font-semibold tabular-nums leading-none">{s.value}</dd>
              <dt className="mt-1 text-[12px] text-muted-foreground">{s.label}</dt>
            </div>
          ))}
        </dl>
      )}
    </div>
  );
}

function EditSheet({ entry, onClose }: { entry: ShowcaseEntry; onClose: () => void }) {
  const qc = useQueryClient();
  const [featured, setFeatured] = useState(entry.featured);
  const [permission, setPermission] = useState(entry.featured);
  const [blurb, setBlurb] = useState(entry.blurb ?? "");
  const [order, setOrder] = useState(String(entry.order));
  const [stats, setStats] = useState<ShowcaseStat[]>(entry.stats);
  const [error, setError] = useState<string | null>(null);
  const active = entry.status === "Active";

  const save = useMutation({
    mutationFn: () => saveShowcase(entry.institutionId, {
      featured, blurb: blurb.trim() || null, order: Number.isFinite(Number(order)) ? Math.trunc(Number(order)) : 0, stats, permissionConfirmed: permission,
    }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["showcase"] });
      toast.success(featured ? `${entry.name} is on the website` : `${entry.name} is not on the website`);
      onClose();
    },
    onError: (e) => setError(handleApiError(e)),
  });

  const toggle = (key: ShowcaseStat, on: boolean) => setStats((s) => on ? [...s, key] : s.filter((x) => x !== key));
  const blocked = featured && (!permission || !active);

  return (
    <FormSheet
      open onClose={onClose} title={entry.name} formId="showcase-form" description="How this institution appears on the AlumUnion website."
      saving={save.isPending} submitDisabled={blocked} error={error}
    >
      <form id="showcase-form" onSubmit={(e) => { e.preventDefault(); setError(null); save.mutate(); }} className="space-y-5">
        <CheckOption id="sc-featured" checked={featured} onChange={setFeatured} label="Show on the website"
          hint={active ? "Visitors to alumunion.com see this institution among the places already using AlumUnion." : "Only an active institution can be shown."} />

        {featured && (
          <>
            <CheckOption id="sc-permission" checked={permission} onChange={setPermission} label="They have agreed to be shown"
              hint="Confirm the institution has said we may use its name and logo, and show the numbers ticked below." />

            <div className="space-y-1.5">
              <label htmlFor="sc-blurb" className="text-[13px] font-medium">One line about them</label>
              <Textarea id="sc-blurb" rows={2} maxLength={BLURB_MAX} value={blurb} onChange={(e) => setBlurb(e.target.value)}
                placeholder="What they do with AlumUnion, in their own terms" />
              <p className="text-right text-[12px] text-muted-foreground tabular-nums">{blurb.length}/{BLURB_MAX}</p>
            </div>

            <fieldset className="space-y-2">
              <legend className="text-[13px] font-medium">Numbers to show</legend>
              <p className="text-[12.5px] text-muted-foreground">Counted live from their own data. A number that is zero is never shown.</p>
              {STATS.map((s) => (
                <CheckOption key={s.key} id={`sc-${s.key}`} checked={stats.includes(s.key)} onChange={(on) => toggle(s.key, on)}
                  label={`${s.label}: ${figure(s.key, entry.numbers)}`} hint={s.hint} />
              ))}
            </fieldset>

            <div className="space-y-1.5">
              <label htmlFor="sc-order" className="text-[13px] font-medium">Position</label>
              <Input id="sc-order" type="number" inputMode="numeric" value={order} onChange={(e) => setOrder(e.target.value)} className="w-28" />
              <p className="text-[12.5px] text-muted-foreground">Lower numbers come first. Ties are shown alphabetically.</p>
            </div>

            <div>
              <p className="mb-2 text-[13px] font-medium">What visitors will see</p>
              <Preview entry={entry} blurb={blurb} stats={stats} />
              <p className="mt-2 text-[12.5px] text-muted-foreground">The website updates within about ten minutes of saving.</p>
            </div>
          </>
        )}
      </form>
    </FormSheet>
  );
}

export default function ShowcasePage() {
  const { isSuperAdmin, role } = useAuth();
  const allowed = isSuperAdmin || role === "Sales";
  const [q, setQ] = useState("");
  const [editing, setEditing] = useState<ShowcaseEntry | null>(null);
  const { data = [], isLoading, isError, refetch } = useQuery({ queryKey: ["showcase"], queryFn: getShowcase, enabled: allowed });

  const featured = useMemo(() => data.filter((e) => e.featured), [data]);
  const term = q.trim().toLowerCase();
  const shown = useMemo(() => (term ? data.filter((e) => e.name.toLowerCase().includes(term) || e.slug.includes(term)) : data), [data, term]);

  if (!allowed) {
    return (
      <div className="p-8 lg:p-12">
        <h1 className="text-2xl font-bold">Unauthorized</h1>
        <p className="mt-2 text-muted-foreground">Only platform staff who handle sales can choose who the website features.</p>
      </div>
    );
  }

  return (
    <div className="max-w-[1000px] p-5 sm:p-7">
      <PageHeading
        title="Website showcase"
        description="The institutions the AlumUnion website names as real examples, with the numbers they have agreed to show. Everything shown is counted from their own data."
      />

      {isError ? <LoadError onRetry={() => refetch()} /> : (
        <>
          <div className="relative mb-4 max-w-md">
            <Search size={15} className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <Input aria-label="Find an institution" placeholder="Find an institution" className="pl-10" value={q} onChange={(e) => setQ(e.target.value)} />
          </div>

          <Card className="overflow-hidden">
            <div className="flex items-center justify-between gap-3 border-b border-border px-5 py-3.5">
              <p className="text-[14px] font-semibold">Institutions</p>
              <p className="text-[13px] text-muted-foreground">{isLoading ? "Loading" : `${featured.length} on the website`}</p>
            </div>
            {isLoading ? (
              <ul className="divide-y divide-border" aria-busy>
                {Array.from({ length: 4 }).map((_, i) => <li key={i} className="flex items-center gap-3 px-5 py-4"><div className="skeleton size-10 rounded-md" /><div className="skeleton h-4 w-48" /></li>)}
              </ul>
            ) : shown.length === 0 ? (
              <EmptyState icon={<Globe size={22} />} title={term ? "No institution matches" : "No institutions yet"} description={term ? "Try another name." : "Institutions appear here once they are onboarded."} />
            ) : (
              <ul className="divide-y divide-border">
                {shown.map((e) => (
                  <li key={e.institutionId} className="flex flex-wrap items-center gap-x-4 gap-y-2 px-5 py-3.5">
                    <Logo entry={e} />
                    <div className="min-w-0 flex-1 basis-48">
                      <p className="truncate text-[14.5px] font-semibold">{e.name}</p>
                      <p className="truncate text-[12.5px] text-muted-foreground">
                        {e.featured
                          ? `On the website${e.featuredAt ? ` since ${formatDate(e.featuredAt)}` : ""}${e.featuredBy ? ` · added by ${e.featuredBy}` : ""}`
                          : e.status !== "Active" ? `${e.status}, so it cannot be shown` : "Not on the website"}
                      </p>
                    </div>
                    <p className="text-[12.5px] tabular-nums text-muted-foreground">
                      {e.numbers.members.toLocaleString()} members · {e.numbers.groups.toLocaleString()} groups · {e.numbers.events.toLocaleString()} events
                    </p>
                    {e.featured && <Badge variant="success">Featured</Badge>}
                    <Button size="sm" variant={e.featured ? "outline" : "default"} onClick={() => setEditing(e)} className={cn(!e.featured && e.status !== "Active" && "opacity-60")}>
                      {e.featured ? "Edit" : "Feature"}
                    </Button>
                  </li>
                ))}
              </ul>
            )}
          </Card>
        </>
      )}

      {editing && <EditSheet key={editing.institutionId} entry={editing} onClose={() => setEditing(null)} />}
    </div>
  );
}
