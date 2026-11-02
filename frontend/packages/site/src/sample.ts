import { STOCK } from "./photos";
import type { SiteData } from "./types";

const day = (n: number) => new Date(Date.now() + n * 86_400_000).toISOString();

/**
 * What live sections show inside the editor, where there may be no real posts or products yet. It is clearly labelled sample data there and
 * is never published: a live site only ever shows the institution's own items.
 */
export const SAMPLE_DATA: SiteData = {
  news: [
    { id: "n1", title: "Reunion dinner announced", excerpt: "Booking opens next week for the main hall.", imageUrl: STOCK.campus, publishedAt: day(-3) },
    { id: "n2", title: "Scholarship fund passes its first target", excerpt: "Thank you to every member who gave.", imageUrl: STOCK.library, publishedAt: day(-9) },
    { id: "n3", title: "New mentorship cohort opens", excerpt: "Twenty places for recent graduates.", imageUrl: STOCK.toss, publishedAt: day(-15) },
    { id: "n4", title: "Annual general meeting minutes", excerpt: "Read what was agreed and what is next.", imageUrl: STOCK.cards[0], publishedAt: day(-22) },
  ],
  events: [
    { id: "e1", title: "Annual reunion dinner", startDate: day(12), venue: "Main hall, 6:00 pm" },
    { id: "e2", title: "Career evening with industry speakers", startDate: day(27), venue: "Online, 7:00 pm" },
    { id: "e3", title: "Carols on the lawn", startDate: day(34), venue: "Campus lawn, 5:30 pm" },
    { id: "e4", title: "Welcome back brunch", startDate: day(60), venue: "Alumni house, 10:00 am" },
  ],
  communities: [
    { id: "c1", name: "Class of 2005", memberCount: 214 }, { id: "c2", name: "Women in Engineering", memberCount: 96 },
    { id: "c3", name: "Accra chapter", memberCount: 388 }, { id: "c4", name: "Football club", memberCount: 54 },
  ],
  businesses: [
    { id: "b1", name: "Amoah Surveying", category: "Engineering" }, { id: "b2", name: "Kofi's Kitchen", category: "Food and catering" },
    { id: "b3", name: "Sarpong Legal", category: "Legal" }, { id: "b4", name: "Ama Designs", category: "Fashion" },
  ],
  services: [
    { id: "s1", name: "Membership certificate", description: "Printed on card and delivered to you.", price: 40 },
    { id: "s2", name: "Transcript request", description: "An official copy sent to the address you give.", price: 60 },
    { id: "s3", name: "Alumni ID card", description: "Photo ID for events and campus visits.", price: 25 },
    { id: "s4", name: "Hall booking", description: "Book the alumni hall at the members' rate.", price: 300 },
  ],
  store: [
    { id: "p1", name: "Alumni t-shirt", price: 80, category: "Apparel", imageUrl: "https://images.unsplash.com/photo-1651761179569-4ba2aa054997?auto=format&fit=crop&w=700&q=70" },
    { id: "p2", name: "Reunion mug", price: 45, category: "Home", imageUrl: "https://images.unsplash.com/photo-1650959858546-d09833d5317b?auto=format&fit=crop&w=700&q=70" },
    { id: "p3", name: "Embroidered cap", price: 55, category: "Apparel", imageUrl: "https://images.unsplash.com/photo-1691256676359-20e5c6d4bc92?auto=format&fit=crop&w=700&q=70" },
    { id: "p4", name: "Canvas tote bag", price: 40, category: "Bags", imageUrl: "https://images.unsplash.com/photo-1574365569389-a10d488ca3fb?auto=format&fit=crop&w=700&q=70" },
  ],
  fundraisers: [
    { id: "f1", title: "Scholarship fund", summary: "Pays fees for students who could not otherwise attend.", raised: 31000, goal: 50000 },
    { id: "f2", title: "Library renovation", summary: "New shelves, desks and a quiet reading room.", raised: 14000, goal: 50000 },
    { id: "f3", title: "Sports kit", summary: "Boots and balls for the campus teams.", raised: 8500, goal: 10000 },
  ],
  jobs: [
    { id: "j1", title: "Operations officer", company: "Volta Logistics", location: "Accra" },
    { id: "j2", title: "Junior geologist", company: "Tarkwa Mining Co.", location: "Tarkwa" },
    { id: "j3", title: "Finance trainee", company: "Greenfield Bank", location: "Kumasi" },
  ],
  stats: { members: 2480, joinedLast30Days: 36, eventsNext30Days: 4, openJobs: 3, businesses: 4, communities: 4 },
};
