import { redirect } from "next/navigation";

/** There is no page for a single event here: its card, with the share and update actions, is on the events list. Old links land on it. */
export default async function EventRedirect({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  redirect(`/events?focus=${encodeURIComponent(id)}`);
}
