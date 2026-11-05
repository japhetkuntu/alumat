import { MessageCircle } from "@alumni/ui";

/** Shown beside the list on a wide screen when no conversation is open. On a phone the list is the page, so this is hidden by the layout. */
export default function ConnectionsHome() {
  return (
    <div className="flex h-full flex-col items-center justify-center gap-3 px-8 text-center">
      <span className="flex h-16 w-16 items-center justify-center rounded-2xl bg-muted"><MessageCircle size={28} className="text-muted-foreground" /></span>
      <p className="font-[family-name:var(--font-display)] text-[18px] font-semibold">Pick a conversation</p>
      <p className="max-w-[34ch] text-[14px] leading-relaxed text-muted-foreground">Choose a chat on the left, or open the directory to ask someone new for an introduction.</p>
    </div>
  );
}
