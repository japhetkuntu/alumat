"use client";

import { useState } from "react";
import { ArrowRight, Briefcase, MapPin, Heart, Check, Button } from "@alumni/ui";
import { trackMarketing } from "./analytics";

const CAMPAIGNS = [
  { id: "library", title: "Build the next chapter", category: "Library fund", description: "Help equip a community library with books and study spaces.", raised: 7500, goal: 12000, supporters: 38, contributions: [{ name: "Ama M.", amount: 500 }, { name: "Class of 2016", amount: 750 }, { name: "Kwame B.", amount: 250 }] },
  { id: "scholarships", title: "Open a door for a student", category: "Scholarship fund", description: "Support the next generation with a community scholarship.", raised: 4200, goal: 10000, supporters: 21, contributions: [{ name: "Class of 2018", amount: 1000 }, { name: "Akosua O.", amount: 200 }, { name: "Engineering chapter", amount: 500 }] },
];
const currency = (amount: number) => `GH₵${amount.toLocaleString("en-GH")}`;

export function FundraisingPreview() {
  const [campaignId, setCampaignId] = useState("library");
  const [showContributions, setShowContributions] = useState(false);
  const campaign = CAMPAIGNS.find(item => item.id === campaignId)!;
  const progress = Math.round(campaign.raised / campaign.goal * 100);
  return <>
    <h3 className="text-xl font-semibold">Give every contribution a record.</h3>
    <p className="text-sm text-muted-foreground mt-2 mb-5">Explore two example campaigns and see how progress and contributions stay together.</p>
    <div className="flex flex-wrap gap-2 mb-5" aria-label="Choose an example campaign">
      {CAMPAIGNS.map(item => <button className="mk-preview-chip" key={item.id} aria-pressed={campaignId === item.id} onClick={() => { setCampaignId(item.id); setShowContributions(false); trackMarketing("product_preview_action", "campaign_select"); }}>{item.category}</button>)}
    </div>
    <div className="mk-preview-card max-w-xl">
      <p className="text-xs text-primary font-semibold uppercase tracking-wide">{campaign.category}</p>
      <h4 className="text-2xl font-semibold mt-3">{campaign.title}</h4>
      <p className="text-sm text-muted-foreground leading-relaxed mt-3">{campaign.description}</p>
      <div className="flex flex-wrap items-end justify-between gap-3 mt-7 mb-3"><p><strong className="text-2xl">{currency(campaign.raised)}</strong><span className="text-xs text-muted-foreground block mt-1">raised of {currency(campaign.goal)}</span></p><span className="text-sm font-semibold text-primary">{progress}% funded</span></div>
      <div role="progressbar" aria-label={`${campaign.category} funding`} aria-valuemin={0} aria-valuemax={100} aria-valuenow={progress} className="h-2.5 rounded-full bg-muted overflow-hidden"><div className="h-full rounded-full bg-primary" style={{width: `${progress}%`}} /></div>
      <div className="flex flex-wrap justify-between items-center gap-3 mt-5"><p className="text-xs text-muted-foreground">{campaign.supporters} example supporters</p><Button variant="outline" size="sm" className="mk-preview-action" aria-expanded={showContributions} onClick={() => { setShowContributions(!showContributions); trackMarketing("product_preview_action", "campaign_records"); }}>{showContributions ? "Hide contributions" : "View contributions"}</Button></div>
      {showContributions && <div className="border-t border-border pt-4 mt-5"><p className="text-xs font-semibold mb-3">Recent example contributions</p>{campaign.contributions.map(item => <div key={item.name} className="flex justify-between py-2 text-sm"><span>{item.name}</span><span className="font-medium">{currency(item.amount)}</span></div>)}<p className="text-xs text-muted-foreground mt-3">A sample of the campaign’s contributions. No payment is collected in this preview.</p></div>}
    </div>
  </>;
}

const JOBS = [
  { id: "engineer", title: "Graduate civil engineer", company: "Example Engineering Ltd", location: "Accra", type: "Full-time", description: "Join a project team working on infrastructure design and site coordination.", requirements: "Civil engineering degree, clear communication, and an interest in fieldwork." },
  { id: "intern", title: "Communications intern", company: "Example Community Foundation", location: "Kumasi", type: "Internship", description: "Help share community stories, plan events, and prepare member updates.", requirements: "Strong writing skills, curiosity, and an interest in community work." },
  { id: "coordinator", title: "Project coordinator", company: "Example Projects Ltd", location: "Tarkwa", type: "Contract", description: "Coordinate project schedules, team updates, and delivery milestones.", requirements: "Project coordination experience and confidence working with different teams." },
];

export function JobsPreview() {
  const [filter, setFilter] = useState("All roles");
  const [selected, setSelected] = useState<string | null>(null);
  const jobs = JOBS.filter(job => filter === "All roles" || job.type === filter);
  const job = JOBS.find(item => item.id === selected);
  return <>
    <h3 className="text-xl font-semibold">Opportunity brings people back.</h3>
    <p className="text-sm text-muted-foreground mt-2 mb-5">Filter the example roles, then open one to explore the details.</p>
    <div className="flex flex-wrap gap-2 mb-5" aria-label="Filter example jobs">{["All roles", "Full-time", "Internship", "Contract"].map(type => <button key={type} className="mk-preview-chip" aria-pressed={filter === type} onClick={() => { setFilter(type); setSelected(null); }}>{type}</button>)}</div>
    <div className="grid gap-3 lg:grid-cols-2">{jobs.map(item => <article className="mk-preview-card" key={item.id}>
      <div className="flex items-center gap-2 text-primary text-xs font-semibold"><Briefcase size={14} />{item.type}</div><h4 className="font-semibold mt-3">{item.title}</h4><p className="text-xs text-muted-foreground mt-2">{item.company}</p><p className="flex items-center gap-1 text-xs text-muted-foreground mt-2"><MapPin size={12} />{item.location}, Ghana</p>
      <Button className="mt-4 mk-preview-action" variant="outline" size="sm" aria-expanded={selected === item.id} onClick={() => { setSelected(selected === item.id ? null : item.id); trackMarketing("product_preview_action", "job_details"); }}>View role <ArrowRight size={13} /></Button>
    </article>)}</div>
    {job && <div className="mk-preview-detail"><h4 className="font-semibold">{job.title}</h4><p className="text-sm text-muted-foreground leading-relaxed mt-2">{job.description}</p><p className="text-xs font-semibold mt-4">What the role calls for</p><p className="text-sm text-muted-foreground leading-relaxed mt-2">{job.requirements}</p><p className="text-xs text-muted-foreground mt-4">Example vacancy. This preview does not accept applications.</p></div>}
  </>;
}

const MENTORS = [
  { id: "ama", name: "Ama Mensah", initials: "AM", role: "Civil engineer", area: "Engineering", experience: "8 years in infrastructure projects", topics: "Career planning, site work, and your first engineering role" },
  { id: "kwame", name: "Kwame Boateng", initials: "KB", role: "Project manager", area: "Leadership", experience: "6 years leading project teams", topics: "Project management, team leadership, and career transitions" },
];

export function MentorshipPreview() {
  const [area, setArea] = useState("All areas");
  const [requested, setRequested] = useState<string[]>([]);
  return <>
    <h3 className="text-xl font-semibold">Experience worth passing on.</h3>
    <p className="text-sm text-muted-foreground mt-2 mb-5">Explore mentors by area and try a request. Everything stays in this preview.</p>
    <div className="flex flex-wrap gap-2 mb-5" aria-label="Filter example mentors">{["All areas", "Engineering", "Leadership"].map(item => <button key={item} className="mk-preview-chip" aria-pressed={area === item} onClick={() => setArea(item)}>{item}</button>)}</div>
    <div className="grid lg:grid-cols-2 gap-3">{MENTORS.filter(mentor => area === "All areas" || mentor.area === area).map(mentor => <article className="mk-preview-card" key={mentor.id}>
      <div className="flex items-center gap-3"><span className="w-11 h-11 rounded-full grid place-items-center bg-primary/10 text-primary text-sm font-semibold">{mentor.initials}</span><div><h4 className="font-semibold">{mentor.name}</h4><p className="text-xs text-muted-foreground mt-1">{mentor.role}</p></div></div>
      <p className="text-xs text-primary font-semibold mt-4 flex items-center gap-2"><Heart size={13} />{mentor.area}</p><p className="text-sm text-muted-foreground mt-3">{mentor.experience}</p><p className="text-xs text-muted-foreground leading-relaxed mt-2">{mentor.topics}</p>
      <Button variant="outline" size="sm" className="mt-5 mk-preview-action" onClick={() => { setRequested(requested.includes(mentor.id) ? requested.filter(id => id !== mentor.id) : [...requested, mentor.id]); trackMarketing("product_preview_action", "mentorship_request"); }}>{requested.includes(mentor.id) ? "Cancel example request" : "Try a mentorship request"}</Button>
      {requested.includes(mentor.id) && <p role="status" className="text-xs text-primary leading-relaxed mt-3 flex gap-2"><Check size={14} className="shrink-0 mt-0.5" />Example request pending. In the portal, the mentor reviews your request before accepting.</p>}
    </article>)}</div>
  </>;
}
