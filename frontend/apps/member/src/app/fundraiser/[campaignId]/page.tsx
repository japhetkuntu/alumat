import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { formatCurrency, formatDate, ZoomableImage } from "@alumni/ui";
import { getInstitutionTheme } from "@/lib/theme";
import { getPublicFundraiser } from "@/lib/public-fundraiser";
import { ContributorNames } from "./contributor-names";

export async function generateMetadata({ params }: { params: Promise<{ campaignId: string }> }): Promise<Metadata> {
  const { campaignId } = await params;
  const [fundraiser, theme] = await Promise.all([getPublicFundraiser(campaignId), getInstitutionTheme()]);
  if (!fundraiser) return { robots: { index: false } };
  const description = fundraiser.description?.slice(0, 160) || `Thank you to everyone supporting ${fundraiser.title}.`;
  return {
    title: fundraiser.title,
    description,
    openGraph: {
      title: fundraiser.title, description, siteName: theme?.displayName,
      images: fundraiser.bannerImageUrl ? [{ url: fundraiser.bannerImageUrl }] : undefined,
    },
  };
}

function daysLeft(deadline: string) {
  return Math.ceil((new Date(deadline).getTime() - Date.now()) / 86_400_000);
}

export default async function FundraiserPublicPage({ params }: { params: Promise<{ campaignId: string }> }) {
  const { campaignId } = await params;
  const [f, theme] = await Promise.all([getPublicFundraiser(campaignId), getInstitutionTheme()]);
  if (!f) notFound();

  const org = theme?.displayName ?? "our community";
  const remaining = f.deadline ? daysLeft(f.deadline) : null;
  const figures: { label: string; value: string }[] = [];
  if (f.totalRaised != null) figures.push({ label: "Contributed", value: formatCurrency(f.totalRaised) });
  if (f.targetAmount != null) figures.push({ label: "Target", value: formatCurrency(f.targetAmount) });
  if (f.contributorCount != null) figures.push({ label: f.contributorCount === 1 ? "Contributor" : "Contributors", value: f.contributorCount.toLocaleString("en-GH") });
  if (f.deadline) figures.push({ label: f.isOpenForGiving ? "Closes" : "Closed", value: formatDate(f.deadline) });

  return (
    <div className="min-h-screen bg-background text-foreground">
      <header className="border-b border-border/60">
        <div className="mx-auto flex max-w-3xl items-center justify-between gap-4 px-5 py-4">
          <Link href="/" className="flex items-center gap-2.5 min-w-0">
            {theme?.logoUrl && <img src={theme.logoUrl} alt="" className="h-8 w-auto shrink-0" />}
            <span className="truncate text-[15px] font-semibold">{org}</span>
          </Link>
          <nav className="flex items-center gap-4 text-[13px] shrink-0">
            <Link href="/login" className="text-muted-foreground hover:text-foreground transition-colors">Sign in</Link>
            <Link href="/register" className="font-semibold text-primary hover:underline">Join</Link>
          </nav>
        </div>
      </header>

      <main className="mx-auto max-w-3xl px-5 pb-16">
        {f.bannerImageUrl && (
          <ZoomableImage src={f.bannerImageUrl} alt="" wrapperClassName="mt-6 aspect-[16/7] bg-muted" className="h-full w-full object-cover" />
        )}

        <div className="pt-10 pb-8 text-center">
          <h1 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">{f.title}</h1>
          {f.description && <p className="mx-auto mt-4 max-w-xl text-[15px] leading-relaxed text-muted-foreground whitespace-pre-line">{f.description}</p>}
        </div>

        {(figures.length > 0 || f.progressPercent != null) && (
          <section aria-label="Progress" className="border-y border-border/60 py-6">
            {figures.length > 0 && (
              <dl className="grid grid-cols-2 gap-y-5 sm:flex sm:justify-between">
                {figures.map((x) => (
                  <div key={x.label} className="text-center sm:text-left">
                    <dd className="text-xl sm:text-2xl font-bold tabular-nums">{x.value}</dd>
                    <dt className="mt-0.5 text-[12px] uppercase tracking-wider text-muted-foreground">{x.label}</dt>
                  </div>
                ))}
              </dl>
            )}
            {f.progressPercent != null && (
              <div className={figures.length > 0 ? "mt-6" : ""}>
                <div className="h-1.5 w-full bg-muted" role="progressbar" aria-valuenow={f.progressPercent} aria-valuemin={0} aria-valuemax={100}>
                  <div className="h-full bg-primary" style={{ width: `${f.progressPercent}%` }} />
                </div>
                <p className="mt-2 text-[12px] text-muted-foreground tabular-nums">
                  {f.progressPercent}% of the target{f.isOpenForGiving && remaining != null && remaining >= 0 && f.deadline ? ` · ${remaining === 0 ? "closes today" : `${remaining} ${remaining === 1 ? "day" : "days"} left`}` : ""}
                </p>
              </div>
            )}
          </section>
        )}

        {f.message && (
          <p className="mx-auto mt-10 max-w-xl text-center text-[16px] leading-relaxed italic text-foreground/80 text-pretty">{f.message}</p>
        )}

        <section className="mt-14" aria-labelledby="with-thanks">
          <h2 id="with-thanks" className="text-center text-[12px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">With thanks to</h2>
          {f.contributors.length > 0 ? (
            <ContributorNames names={f.contributors} />
          ) : (
            <p className="mt-6 text-center text-[14px] text-muted-foreground">
              {f.isOpenForGiving ? "Be the first name here." : "No names to show."}
            </p>
          )}
        </section>

        {f.isOpenForGiving && (
          <div className="mt-14 text-center">
            <Link
              href={`/payment-campaign/${f.id}`}
              className="inline-flex items-center justify-center bg-primary px-7 py-3 text-[14px] font-semibold text-primary-foreground transition-opacity hover:opacity-90"
            >
              Give to this fundraiser
            </Link>
            <p className="mt-3 text-[13px] text-muted-foreground">
              Members get more —{" "}
              <Link href="/register" className="font-semibold text-foreground underline underline-offset-2 decoration-border hover:decoration-foreground">join {org}</Link>
              {" "}or{" "}
              <Link href="/login" className="font-semibold text-foreground underline underline-offset-2 decoration-border hover:decoration-foreground">sign in</Link>
              {" "}for events, news and the directory.
            </p>
          </div>
        )}
      </main>

      <footer className="border-t border-border/60">
        <p className="mx-auto max-w-3xl px-5 py-6 text-[12px] leading-relaxed text-muted-foreground text-center text-pretty">
          {org} runs on <a href="https://alumunion.com" className="font-medium text-foreground/80 hover:underline">AlumUnion</a>, a platform that helps alumni and community groups keep their dues, giving, events and member directory in one place.
        </p>
      </footer>
    </div>
  );
}
