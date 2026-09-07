import { redirect } from "next/navigation";

// Settings merged into /profile — one "manage my account" destination
// instead of two. Kept as a redirect (not deleted) so old links/bookmarks
// still land somewhere sensible.
export default function SettingsRedirectPage() {
  redirect("/profile");
}
