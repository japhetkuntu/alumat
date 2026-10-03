"use client";

import { useEffect, useRef, useState } from "react";
import { Search, Users, Calendar, CreditCard, Briefcase, Heart, Bell, LayoutDashboard, ArrowRight, Input, Button } from "@alumni/ui";
import { DirectoryMemberCard, type DirectoryMember } from "@/components/member/directory-member-card";
import { MemberEventCard } from "@/components/member/member-event-card";
import type { AlumniEvent } from "@/types";
import { FundraisingPreview, JobsPreview, MentorshipPreview, UpdatesPreview } from "./preview-features";
import { trackMarketing } from "./analytics";

const MEMBERS: DirectoryMember[] = [
  { id: "example-1", firstName: "Ama", lastName: "Mensah", jobTitle: "Civil engineer", location: "Accra, Ghana", graduationYear: 2016, departmentName: "Engineering" },
  { id: "example-2", firstName: "Kwame", lastName: "Boateng", jobTitle: "Project manager", location: "Kumasi, Ghana", graduationYear: 2018, departmentName: "Business" },
  { id: "example-3", firstName: "Akosua", lastName: "Owusu", jobTitle: "Researcher", location: "Tarkwa, Ghana", graduationYear: 2016, departmentName: "Engineering" },
];
const EVENT: AlumniEvent = {
  id: "example-event", title: "Community career evening", venue: "Community hall, Accra",
  startDate: "2027-03-20T17:00:00Z", status: "Upcoming", isTicketed: false,
  rsvpCount: 24, capacity: 60, createdAt: "2026-09-30T00:00:00Z",
};

export function ProductTour() {
  const [view, setView] = useState<"dashboard" | "directory" | "events" | "fundraising" | "jobs" | "updates" | "mentorship">("dashboard");
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState<DirectoryMember | null>(null);
  const [going, setGoing] = useState(false);
  // On a phone the tab strip scrolls sideways, so keep the chosen tab in view.
  const navRef = useRef<HTMLElement>(null);
  useEffect(() => {
    const active = navRef.current?.querySelector<HTMLElement>('[aria-pressed="true"]');
    const nav = navRef.current;
    if (!active || !nav || nav.scrollWidth <= nav.clientWidth) return;
    const a = active.getBoundingClientRect();
    const n = nav.getBoundingClientRect();
    nav.scrollBy({ left: a.left - n.left - (n.width - a.width) / 2 });
  }, [view]);
  const members = MEMBERS.filter(m => `${m.firstName} ${m.lastName} ${m.location} ${m.graduationYear} ${m.jobTitle}`.toLowerCase().includes(search.toLowerCase().trim()));
  return (
    <div className="border border-border bg-background rounded-2xl overflow-hidden">
      <div className="mk-tour-header flex items-center justify-between gap-3 border-b border-border px-4 sm:px-5 py-3 sm:py-4 bg-muted/40">
        <div className="flex items-center gap-2"><img src="/alumunion-mark.svg" alt="" width={24} height={24} /><span className="text-sm font-semibold whitespace-nowrap">Member portal</span></div>
        <span className="text-xs text-muted-foreground whitespace-nowrap"><span className="hidden sm:inline">Interactive preview · </span>Example data</span>
      </div>
      <div className="grid md:grid-cols-[190px_1fr]">
        <nav ref={navRef} aria-label="Product preview" className="mk-preview-nav flex md:flex-col gap-1 p-2 md:p-3 border-b md:border-b-0 md:border-r border-border bg-muted/20">
          {([{ id: "dashboard", label: "My home", icon: LayoutDashboard }, { id: "directory", label: "Member directory", icon: Users }, { id: "events", label: "Events & RSVPs", icon: Calendar }, { id: "fundraising", label: "Campaigns", icon: CreditCard }, { id: "jobs", label: "Jobs", icon: Briefcase }, { id: "updates", label: "Community updates", icon: Bell }, { id: "mentorship", label: "Mentorship", icon: Heart }] as const).map(item => (
            <button key={item.id} type="button" aria-pressed={view === item.id} onClick={() => { setView(item.id); trackMarketing("product_preview_view", item.id); }}
              className={`flex shrink-0 md:shrink md:flex-none items-center gap-2 whitespace-nowrap md:whitespace-normal px-3 py-2.5 md:py-3 rounded-lg text-left text-sm font-medium focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary ${view === item.id ? "bg-primary/10 text-primary" : "text-muted-foreground hover:bg-muted"}`}>
              <item.icon size={15} />{item.label}
            </button>
          ))}
        </nav>
        <div className="mk-tour-body p-4 sm:p-7 min-w-0" aria-live="polite">
          {view === "dashboard" ? <>
            <h3 className="text-xl font-semibold">Everything that matters, when you arrive.</h3>
            <p className="text-sm text-muted-foreground mt-2 mb-5">A simple member home brings the next useful actions together.</p>
            <div className="grid gap-2.5 sm:gap-3 sm:grid-cols-2 lg:grid-cols-3">
              <button type="button" className="mk-preview-card mk-preview-link text-left hover:border-primary/50 transition-colors" onClick={() => setView("events")}><Calendar size={18} className="text-primary" /><span className="mk-preview-link-text"><span className="block font-semibold">Career evening</span><span className="block text-xs text-muted-foreground mt-1">20 March · 24 people going</span><span className="mk-preview-link-cta text-xs text-primary font-semibold">View event <ArrowRight size={12} /></span></span></button>
              <button type="button" className="mk-preview-card mk-preview-link text-left hover:border-primary/50 transition-colors" onClick={() => setView("fundraising")}><CreditCard size={18} className="text-primary" /><span className="mk-preview-link-text"><span className="block font-semibold">Library fund</span><span className="block text-xs text-muted-foreground mt-1">GH₵7,500 of GH₵12,000 raised</span><span className="mk-preview-link-cta text-xs text-primary font-semibold">See progress <ArrowRight size={12} /></span></span></button>
              <button type="button" className="mk-preview-card mk-preview-link text-left hover:border-primary/50 transition-colors" onClick={() => setView("jobs")}><Briefcase size={18} className="text-primary" /><span className="mk-preview-link-text"><span className="block font-semibold">3 new jobs</span><span className="block text-xs text-muted-foreground mt-1">Roles shared for your community</span><span className="mk-preview-link-cta text-xs text-primary font-semibold">Explore jobs <ArrowRight size={12} /></span></span></button>
            </div>
            <div className="mk-preview-detail mt-5"><p className="text-sm font-semibold">New from your community</p><p className="text-sm text-muted-foreground mt-2">The September gathering recap and photos are ready.</p><Button variant="outline" size="sm" className="mt-4 mk-preview-action" onClick={() => setView("updates")}>Read updates</Button></div>
          </> : view === "directory" ? <>
            <h3 className="text-xl font-semibold">Find your people.</h3>
            <p className="text-sm text-muted-foreground mt-2 mb-5">Try searching for “Accra” or “2016”, then select a member.</p>
            <div className="relative mb-5 max-w-md"><Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
              <Input aria-label="Search example members" value={search} onChange={e => { setSearch(e.target.value); setSelected(null); }} placeholder="Search name, city or year" className="pl-9" />
            </div>
            <p className="text-xs text-muted-foreground mb-3">{members.length} example {members.length === 1 ? "member" : "members"}</p>
            <div className="grid gap-3 lg:grid-cols-2">{members.map(member => <DirectoryMemberCard key={member.id} member={member} selected={selected?.id === member.id}
              onSelect={() => { setSelected(selected?.id === member.id ? null : member); trackMarketing("product_preview_action", "member_profile"); }} />)}</div>
            {members.length === 0 && <div className="py-8 text-center"><p className="text-sm text-muted-foreground mb-3">No example members match that search.</p><Button variant="outline" size="sm" onClick={() => setSearch("")}>Clear search</Button></div>}
            {selected && <div className="mt-4 border-l-2 border-primary pl-4 py-2"><p className="font-semibold">{selected.firstName} {selected.lastName}</p><p className="text-sm text-muted-foreground mt-1">{selected.jobTitle} · {selected.location} · Class of {selected.graduationYear}</p><p className="text-xs text-muted-foreground mt-2">Example profile. No personal contact details are shared in this preview.</p></div>}
          </> : view === "events" ? <>
            <h3 className="text-xl font-semibold">From invitation to RSVP.</h3>
            <p className="text-sm text-muted-foreground mt-2 mb-5">Try reserving a place. This preview makes no real booking.</p>
            <div className="max-w-sm"><MemberEventCard event={{...EVENT, rsvpCount: EVENT.rsvpCount + (going ? 1 : 0)}} hasRsvp={going} isPast={false} canRsvp isFull={false} isPending={false}
              onRsvp={() => { setGoing(!going); trackMarketing("product_preview_action", going ? "cancel_example_rsvp" : "example_rsvp"); }} /></div>
            <p role="status" className="mt-4 text-sm text-muted-foreground">{going ? "Example RSVP confirmed. Your place is reserved in this preview only." : "Members can see event details and manage their RSVP from the same place."}</p>
          </> : view === "fundraising" ? <FundraisingPreview /> : view === "jobs" ? <JobsPreview /> : view === "updates" ? <UpdatesPreview /> : <MentorshipPreview />}
        </div>
      </div>
      <p className="px-4 sm:px-5 py-3 border-t border-border text-xs text-muted-foreground">Seven member-facing experiences. Nothing here sends a payment, application or request.</p>
    </div>
  );
}
