import Link from "next/link";
import { EmptyState } from "@alumni/ui";

/** Shown instead of a misleading "couldn't load" when someone opens a workspace that belongs to another kind of administrator. */
export function WrongWorkspace({ title, description, href, label }: { title: string; description: string; href: string; label: string }) {
  return (
    <div className="p-4 sm:p-[26px] max-w-[1000px] mx-auto">
      <EmptyState title={title} description={description} action={<Link href={href} className="font-semibold text-primary underline underline-offset-4">{label}</Link>} />
    </div>
  );
}
