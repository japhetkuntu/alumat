"use client";

import { useRef, useState, useCallback } from "react";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Download, Award, CheckCircle2 } from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import umatLogo from "@/app/umatLogo.png";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { CardSkeleton } from "@/components/ui/skeleton";
import { formatCurrency, formatDate } from "@/lib/utils";
import { getMyProfile, getMyContributions, getMyCampaigns, getMyMembershipStatus } from "@/lib/member-api";
import type { Campaign, Contribution } from "@/types";

interface PaidMembership {
  campaign: Campaign;
  contribution: Contribution;
}

export default function MembershipCertificatePage() {
  const certRef = useRef<HTMLDivElement>(null);
  const [downloading, setDownloading] = useState(false);
  const [selectedYear, setSelectedYear] = useState<number | null>(null);

  const { data: profile, isLoading: loadingProfile } = useQuery({
    queryKey: ["m-profile"],
    queryFn: getMyProfile,
  });

  const { data: membershipStatus } = useQuery({
    queryKey: ["m-membership-status"],
    queryFn: getMyMembershipStatus,
    retry: false,
  });

  const { data: campaignsData, isLoading: loadingCampaigns } = useQuery({
    queryKey: ["m-campaigns"],
    queryFn: () => getMyCampaigns(1, 100),
  });

  const { data: contributionsData, isLoading: loadingContribs } = useQuery({
    queryKey: ["m-contributions-all"],
    queryFn: () => getMyContributions({ pageSize: 500 }),
  });

  const isLoading = loadingProfile || loadingCampaigns || loadingContribs;

  // Build list of paid membership campaigns with their confirmed contributions
  const paidMemberships: PaidMembership[] = (() => {
    if (!campaignsData?.results || !contributionsData?.results) return [];
    const membershipCampaigns = campaignsData.results.filter((c) => c.isMembershipCampaign);
    const confirmedContribs = contributionsData.results.filter((c) => c.status === "Confirmed");

    return membershipCampaigns
      .map((campaign) => {
        const contribution = confirmedContribs.find((c) => c.campaignId === campaign.id);
        return contribution ? { campaign, contribution } : null;
      })
      .filter((x): x is PaidMembership => x !== null)
      .sort((a, b) => (b.campaign.membershipYear ?? 0) - (a.campaign.membershipYear ?? 0));
  })();

  const selected = selectedYear
    ? paidMemberships.find((p) => p.campaign.membershipYear === selectedYear)
    : paidMemberships[0];

  // Default to first available
  const activeYear = selected?.campaign.membershipYear ?? paidMemberships[0]?.campaign.membershipYear;

  const handleDownload = useCallback(async () => {
    if (!certRef.current || !selected) return;
    setDownloading(true);
    try {
      const html2canvas = (await import("html2canvas-pro")).default;
      const { jsPDF } = await import("jspdf");

      const el = certRef.current;

      // Force desktop-size rendering regardless of screen size
      const originalStyle = el.style.cssText;
      el.style.width = "840px";
      el.style.height = "594px";
      el.style.position = "fixed";
      el.style.left = "-9999px";
      el.style.top = "0";
      // Force a reflow so the browser recalculates layout at 840px
      el.offsetHeight;

      const canvas = await html2canvas(el, {
        scale: 3,
        useCORS: true,
        backgroundColor: "#ffffff",
        logging: false,
      });

      // Restore original styles
      el.style.cssText = originalStyle;

      // A4 landscape for certificate feel
      const pdf = new jsPDF({ orientation: "landscape", unit: "mm", format: "a4" });
      const pageW = pdf.internal.pageSize.getWidth();
      const pageH = pdf.internal.pageSize.getHeight();

      const imgW = canvas.width;
      const imgH = canvas.height;
      const ratio = Math.min(pageW / imgW, pageH / imgH);
      const w = imgW * ratio;
      const h = imgH * ratio;
      const x = (pageW - w) / 2;
      const y = (pageH - h) / 2;

      pdf.addImage(canvas.toDataURL("image/png"), "PNG", x, y, w, h);
      pdf.save(`Membership-Certificate-${selected.campaign.membershipYear}-${profile?.firstName ?? "Member"}-${profile?.lastName ?? ""}.pdf`);
    } catch {
      // Fallback: use browser print
      window.print();
    } finally {
      setDownloading(false);
    }
  }, [selected, profile]);

  if (isLoading) {
    return (
      <div className="p-4 sm:p-6 lg:p-8 space-y-6 max-w-5xl mx-auto">
        <CardSkeleton />
        <CardSkeleton />
      </div>
    );
  }

  if (paidMemberships.length === 0) {
    return (
      <div className="px-5 py-4 sm:p-6 lg:p-8 space-y-6 max-w-5xl mx-auto">
        <div className="flex items-center gap-3">
          <Link href="/dashboard"><Button size="sm" variant="ghost"><ArrowLeft size={14} />Back</Button></Link>
          <h1 className="text-2xl font-bold">Membership Certificate</h1>
        </div>
        <Card>
          <CardContent className="p-8 sm:p-12 text-center space-y-4">
            <Award size={48} className="mx-auto text-muted-foreground/40" />
            <h2 className="text-lg font-bold text-muted-foreground">No Certificates Available</h2>
            <p className="text-sm text-muted-foreground max-w-md mx-auto">
              You haven&apos;t paid for any membership campaigns yet. Once you make a membership payment, your certificate will be available here.
            </p>
            <Link href="/contributions">
              <Button variant="outline">View Campaigns</Button>
            </Link>
          </CardContent>
        </Card>
      </div>
    );
  }

  const memberName = `${profile?.firstName ?? ""} ${profile?.lastName ?? ""}`.trim() || "Alumni Member";
  const memberNumber = profile?.memberNumber ?? profile?.id?.slice(0, 8).toUpperCase() ?? "";
  const graduationYear = profile?.graduationYear;
  const paymentDate = selected?.contribution.confirmedAt ?? selected?.contribution.createdAt;

  return (
    <div className="p-4 sm:p-6 lg:p-8 space-y-6 max-w-5xl mx-auto">
      {/* Navigation */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 animate-in fade-in slide-in-from-bottom-3 duration-700">
        <div className="flex items-center gap-3">
          <Link href="/dashboard"><Button size="sm" variant="ghost"><ArrowLeft size={14} />Back</Button></Link>
          <div>
            <h1 className="text-2xl font-bold tracking-tight">Membership Certificate</h1>
            <p className="text-sm text-muted-foreground">View and download your membership certificates</p>
          </div>
        </div>
        <Button onClick={handleDownload} disabled={downloading} className="font-bold shadow-lg shadow-primary/20">
          <Download size={16} />{downloading ? "Generating PDF..." : "Download PDF"}
        </Button>
      </div>

      {/* Year Selector */}
      {paidMemberships.length > 1 && (
        <div className="flex flex-wrap gap-2 animate-in fade-in slide-in-from-bottom-3 duration-700 delay-100">
          {paidMemberships.map((p) => (
            <button
              key={p.campaign.membershipYear}
              onClick={() => setSelectedYear(p.campaign.membershipYear ?? null)}
              className={`px-4 py-2 rounded-full text-sm font-bold transition-all ${
                (p.campaign.membershipYear === activeYear)
                  ? "bg-primary text-primary-foreground shadow-md shadow-primary/20"
                  : "bg-muted/50 text-muted-foreground hover:bg-muted"
              }`}
            >
              {p.campaign.membershipYear}
            </button>
          ))}
        </div>
      )}

      {/* Certificate */}
      {selected && (
        <div className="animate-in fade-in slide-in-from-bottom-4 duration-700 delay-200">
          <div
            ref={certRef}
            className="relative bg-white rounded-2xl overflow-hidden shadow-2xl"
            style={{ aspectRatio: "297/210", containerType: "inline-size" }}
          >
            {/* Decorative border */}
            <div className="absolute inset-0" style={{ padding: 'clamp(4px, 2.1cqw, 20px)' }}>
              <div className="absolute border sm:border-2 border-amber-600/30 rounded-lg sm:rounded-xl" style={{ inset: 'clamp(4px, 2.1cqw, 20px)' }} />
              <div className="absolute border border-amber-600/15 rounded sm:rounded-lg" style={{ inset: 'clamp(8px, 2.5cqw, 24px)' }} />
            </div>

            {/* Corner ornaments */}
            {[['top', 'left', 'border-t', 'border-l', 'rounded-tl'], ['top', 'right', 'border-t', 'border-r', 'rounded-tr'], ['bottom', 'left', 'border-b', 'border-l', 'rounded-bl'], ['bottom', 'right', 'border-b', 'border-r', 'rounded-br']].map(([v, h, bt, bl, r]) => (
              <div key={`${v}-${h}`} className={`absolute ${bt}-2 ${bl}-2 sm:${bt}-3 sm:${bl}-3 border-amber-600/40 ${r}`} style={{ [v]: 'clamp(8px, 3.4cqw, 32px)', [h]: 'clamp(8px, 3.4cqw, 32px)', width: 'clamp(12px, 5cqw, 48px)', height: 'clamp(12px, 5cqw, 48px)' }} />
            ))}

            {/* Subtle pattern background */}
            <div className="absolute inset-0 opacity-[0.03]" style={{ backgroundImage: "radial-gradient(circle at 25px 25px, #000 1px, transparent 1px)", backgroundSize: "50px 50px" }} />

            {/* Content */}
            <div className="relative h-full flex flex-col items-center justify-center text-center" style={{ padding: 'clamp(8px, 4.2cqw, 40px) clamp(12px, 5cqw, 48px)' }}>
              {/* Header */}
              <div className="flex items-center" style={{ gap: 'clamp(4px, 1.3cqw, 12px)', marginBottom: 'clamp(4px, 1.7cqw, 16px)' }}>
                <Image src={umatLogo} alt="UMaT Logo" width={48} height={48} className="object-contain" style={{ width: 'clamp(16px, 5cqw, 48px)', height: 'clamp(16px, 5cqw, 48px)' }} />
                <div>
                  <h2 className="font-bold text-amber-800 tracking-wider" style={{ fontSize: 'clamp(7px, 2.1cqw, 20px)' }}>UMaT ALUMNI ASSOCIATION</h2>
                </div>
              </div>

              <div className="h-px bg-gradient-to-r from-transparent via-amber-600/40 to-transparent" style={{ width: 'clamp(24px, 10cqw, 96px)', marginBottom: 'clamp(4px, 2.1cqw, 20px)' }} />

              <h1 className="font-black text-gray-900 tracking-tight" style={{ fontSize: 'clamp(10px, 5cqw, 48px)', marginBottom: 'clamp(2px, 0.9cqw, 8px)' }}>
                Certificate of Membership
              </h1>
              <p className="text-gray-500 font-medium tracking-widest uppercase" style={{ fontSize: 'clamp(5px, 1.5cqw, 14px)', marginBottom: 'clamp(6px, 3.4cqw, 32px)' }}>
                {selected.campaign.membershipYear} Membership Year
              </p>

              {/* Member name */}
              <p className="text-gray-500" style={{ fontSize: 'clamp(5px, 1.5cqw, 14px)', marginBottom: 'clamp(2px, 0.9cqw, 8px)' }}>This certifies that</p>
              <h2 className="font-black text-primary" style={{ fontSize: 'clamp(9px, 3.8cqw, 36px)', marginBottom: 'clamp(2px, 0.9cqw, 8px)', fontFamily: "Georgia, 'Times New Roman', serif" }}>
                {memberName}
              </h2>
              <div className="h-px bg-gradient-to-r from-transparent via-gray-300 to-transparent" style={{ width: 'clamp(60px, 30cqw, 288px)', marginBottom: 'clamp(6px, 2.5cqw, 24px)' }} />

              {/* Details */}
              <div className="flex flex-wrap items-center justify-center max-w-2xl w-full" style={{ gap: 'clamp(3px, 2.1cqw, 20px)', marginBottom: 'clamp(6px, 3.4cqw, 32px)' }}>
                <div className="text-center" style={{ padding: '0 clamp(2px, 1.3cqw, 12px)' }}>
                  <p className="font-bold uppercase text-amber-600/60" style={{ fontSize: 'clamp(4px, 1cqw, 9px)', letterSpacing: '0.15em' }}>Member ID</p>
                  <p className="font-extrabold text-gray-800" style={{ fontSize: 'clamp(6px, 1.5cqw, 14px)' }}>{memberNumber}</p>
                </div>
                <span className="text-amber-300 select-none" style={{ fontSize: 'clamp(4px, 1.3cqw, 12px)' }}>•</span>
                {graduationYear && (
                  <>
                    <div className="text-center" style={{ padding: '0 clamp(2px, 1.3cqw, 12px)' }}>
                      <p className="font-bold uppercase text-amber-600/60" style={{ fontSize: 'clamp(4px, 1cqw, 9px)', letterSpacing: '0.15em' }}>Class of</p>
                      <p className="font-extrabold text-gray-800" style={{ fontSize: 'clamp(6px, 1.5cqw, 14px)' }}>{graduationYear}</p>
                    </div>
                    <span className="text-amber-300 select-none" style={{ fontSize: 'clamp(4px, 1.3cqw, 12px)' }}>•</span>
                  </>
                )}
                <div className="text-center" style={{ padding: '0 clamp(2px, 1.3cqw, 12px)' }}>
                  <p className="font-bold uppercase text-amber-600/60" style={{ fontSize: 'clamp(4px, 1cqw, 9px)', letterSpacing: '0.15em' }}>Amount</p>
                  <p className="font-extrabold text-gray-800" style={{ fontSize: 'clamp(6px, 1.5cqw, 14px)' }}>{formatCurrency(selected.contribution.amount)}</p>
                </div>
                <span className="text-amber-300 select-none" style={{ fontSize: 'clamp(4px, 1.3cqw, 12px)' }}>•</span>
                <div className="text-center" style={{ padding: '0 clamp(2px, 1.3cqw, 12px)' }}>
                  <p className="font-bold uppercase text-amber-600/60" style={{ fontSize: 'clamp(4px, 1cqw, 9px)', letterSpacing: '0.15em' }}>Paid on</p>
                  <p className="font-extrabold text-gray-800" style={{ fontSize: 'clamp(6px, 1.5cqw, 14px)' }}>{paymentDate ? formatDate(paymentDate) : "—"}</p>
                </div>
              </div>

              {/* Verification badge */}
              <div className="flex items-center rounded-full bg-green-50 border border-green-200" style={{ gap: 'clamp(2px, 0.9cqw, 8px)', padding: 'clamp(2px, 0.9cqw, 8px) clamp(6px, 1.7cqw, 16px)' }}>
                <CheckCircle2 className="text-green-600" style={{ width: 'clamp(8px, 1.7cqw, 16px)', height: 'clamp(8px, 1.7cqw, 16px)' }} />
                <span className="font-bold text-green-700 tracking-wide" style={{ fontSize: 'clamp(5px, 1.3cqw, 12px)' }}>Payment Verified &amp; Confirmed</span>
              </div>

              {/* Footer */}
              <div className="flex flex-col items-center gap-1" style={{ marginTop: 'clamp(4px, 3.4cqw, 32px)' }}>
                <p className="text-gray-300 font-medium tracking-wider" style={{ fontSize: 'clamp(4px, 1.1cqw, 10px)' }}>
                  Ref: {selected.contribution.transactionRef ?? selected.contribution.id.slice(0, 12).toUpperCase()}
                </p>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Certificate Details Card */}
      {selected && (
        <Card className="animate-in fade-in slide-in-from-bottom-4 duration-700 delay-300">
          <CardContent className="p-3 sm:p-5 lg:p-6">
            <h3 className="text-xs sm:text-sm font-bold mb-3 sm:mb-4 flex items-center gap-2">
              <Award size={16} className="text-primary" />Payment Details
            </h3>
            <div className="grid grid-cols-2 sm:grid-cols-2 lg:grid-cols-4 gap-2 sm:gap-4">
              <div className="p-2 sm:p-3 rounded-lg bg-muted/30">
                <p className="text-[9px] sm:text-[10px] font-bold uppercase tracking-widest text-muted-foreground mb-0.5 sm:mb-1">Campaign</p>
                <p className="text-xs sm:text-sm font-bold truncate">{selected.campaign.title}</p>
              </div>
              <div className="p-2 sm:p-3 rounded-lg bg-muted/30">
                <p className="text-[9px] sm:text-[10px] font-bold uppercase tracking-widest text-muted-foreground mb-0.5 sm:mb-1">Membership Year</p>
                <p className="text-xs sm:text-sm font-bold">{selected.campaign.membershipYear}</p>
              </div>
              <div className="p-2 sm:p-3 rounded-lg bg-muted/30">
                <p className="text-[9px] sm:text-[10px] font-bold uppercase tracking-widest text-muted-foreground mb-0.5 sm:mb-1">Amount Paid</p>
                <p className="text-xs sm:text-sm font-bold text-green-600">{formatCurrency(selected.contribution.amount)}</p>
              </div>
              <div className="p-2 sm:p-3 rounded-lg bg-muted/30">
                <p className="text-[9px] sm:text-[10px] font-bold uppercase tracking-widest text-muted-foreground mb-0.5 sm:mb-1">Payment Method</p>
                <p className="text-xs sm:text-sm font-bold">{selected.contribution.paymentMethod}</p>
              </div>
              <div className="p-2 sm:p-3 rounded-lg bg-muted/30">
                <p className="text-[9px] sm:text-[10px] font-bold uppercase tracking-widest text-muted-foreground mb-0.5 sm:mb-1">Status</p>
                <Badge variant="success" className="font-bold text-[10px] sm:text-xs">{selected.contribution.status}</Badge>
              </div>
              <div className="p-2 sm:p-3 rounded-lg bg-muted/30">
                <p className="text-[9px] sm:text-[10px] font-bold uppercase tracking-widest text-muted-foreground mb-0.5 sm:mb-1">Payment Date</p>
                <p className="text-xs sm:text-sm font-bold">{paymentDate ? formatDate(paymentDate) : "—"}</p>
              </div>
              <div className="p-2 sm:p-3 rounded-lg bg-muted/30 col-span-2">
                <p className="text-[9px] sm:text-[10px] font-bold uppercase tracking-widest text-muted-foreground mb-0.5 sm:mb-1">Transaction Reference</p>
                <p className="text-xs sm:text-sm font-bold font-mono truncate">{selected.contribution.transactionRef ?? "N/A"}</p>
              </div>
            </div>
            {membershipStatus && (
              <div className="mt-3 sm:mt-4 p-2 sm:p-3 rounded-lg border border-primary/20 bg-primary/5">
                <p className="text-[10px] sm:text-xs font-bold text-primary flex items-center gap-1.5 sm:gap-2">
                  <CheckCircle2 size={14} className="shrink-0" />
                  <span>Membership Status: {membershipStatus.isMembershipActive ? "Active" : "Expired"}
                  {membershipStatus.membershipExpiry && ` — Valid until ${formatDate(membershipStatus.membershipExpiry)}`}</span>
                </p>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* Print-only styles */}
      <style jsx global>{`
        @media print {
          body * { visibility: hidden !important; }
          [data-cert-print], [data-cert-print] * { visibility: visible !important; }
          [data-cert-print] { position: fixed; top: 0; left: 0; width: 100vw; height: 100vh; }
        }
      `}</style>
    </div>
  );
}
