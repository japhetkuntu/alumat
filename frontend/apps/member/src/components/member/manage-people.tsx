"use client";

import { memo, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button, CheckOption, FormSheet, Input, Search, Textarea, UserAvatar, formatDate, formatDateTime } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { decideJoinRequest, messageGroup, type GroupMember, type ManageGroup } from "@/lib/member-api";
import { ListState, Panel, ROW, errorText } from "@/components/member/manage-ui";
import { manageQueries } from "@/components/member/manage-queries";

function Stat({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="rounded-[16px] bg-muted/50 p-4">
      <p className="text-[12px] font-medium text-muted-foreground">{label}</p>
      <p className="mt-1 text-[26px] font-semibold leading-none tabular-nums">{value}</p>
      {hint && <p className="mt-1.5 text-[12px] text-muted-foreground">{hint}</p>}
    </div>
  );
}

export function Report({ group }: { group: ManageGroup }) {
  const { data, isLoading, isError, refetch } = useQuery(manageQueries.summary(group));
  return (
    <Panel id="mg-report" title="How the group is doing">
      <ListState loading={isLoading} error={isError || (!isLoading && !data)} onRetry={() => refetch()} empty={false} emptyText="">
        {data && (
          <div className="mt-3 grid grid-cols-2 gap-3 lg:grid-cols-4">
            <Stat label="Members" value={data.totalMembers.toLocaleString()} hint={data.pendingRequests > 0 ? `${data.pendingRequests} waiting` : undefined} />
            <Stat label="Joined recently" value={data.newLast30Days.toLocaleString()} hint="in the last 30 days" />
            <Stat label="Active" value={data.activeLast30Days.toLocaleString()} hint="signed in, last 30 days" />
            <Stat label="Profiles complete" value={`${data.profileCompletionPercent}%`} hint="on average" />
          </div>
        )}
      </ListState>
    </Panel>
  );
}

const MemberRow = memo(function MemberRow({ m, busy, onDecide }: { m: GroupMember; busy: boolean; onDecide: (m: GroupMember, approve: boolean) => void }) {
  return (
    <li className={`flex items-center gap-3 py-3 ${ROW}`}>
      <UserAvatar src={m.profilePictureUrl} name={m.name} size="default" />
      <div className="min-w-0 flex-1">
        <p className="truncate text-[14px] font-semibold leading-tight">{m.name}</p>
        <p className="truncate text-[12.5px] text-muted-foreground">{[m.jobTitle, m.status === "Pending" ? `Asked ${formatDate(m.joinedAt)}` : `Joined ${formatDate(m.joinedAt)}`].filter(Boolean).join(" · ")}</p>
      </div>
      {m.canDecide && (
        <div className="flex shrink-0 gap-2">
          {m.canDecline && <Button size="sm" variant="ghost" disabled={busy} onClick={() => onDecide(m, false)}>Decline</Button>}
          <Button size="sm" disabled={busy} onClick={() => onDecide(m, true)}>Approve</Button>
        </div>
      )}
    </li>
  );
});

export function Members({ group }: { group: ManageGroup }) {
  const qc = useQueryClient();
  const key = manageQueries.members(group).queryKey;
  const { data = [], isLoading, isError, refetch } = useQuery(manageQueries.members(group));
  const [busy, setBusy] = useState<string | null>(null);

  // The list changes the instant someone is approved or declined, and goes back if the server says no.
  const decide = useMutation({
    mutationFn: ({ m, approve }: { m: GroupMember; approve: boolean }) => decideJoinRequest(group, m.memberId, approve),
    onMutate: async ({ m, approve }) => {
      setBusy(m.memberId);
      await qc.cancelQueries({ queryKey: key });
      const before = qc.getQueryData<GroupMember[]>(key);
      qc.setQueryData<GroupMember[]>(key, (list) => (list ?? []).flatMap((x) => x.memberId !== m.memberId ? [x] : approve ? [{ ...x, status: "Active" as const, canDecide: false, canDecline: false }] : []));
      return { before };
    },
    onSuccess: (_r, { m, approve }) => toast.success(approve ? `${m.name} is in` : `${m.name}'s request was declined`),
    onError: (e, _v, ctx) => { qc.setQueryData(key, ctx?.before); toast.error(errorText(e) ?? "That didn't work"); },
    onSettled: () => {
      setBusy(null);
      qc.invalidateQueries({ queryKey: key });
      qc.invalidateQueries({ queryKey: manageQueries.summary(group).queryKey });
    },
  });
  const onDecide = useMemo(() => (m: GroupMember, approve: boolean) => decide.mutate({ m, approve }), [decide]);

  const [find, setFind] = useState("");
  const term = find.trim().toLowerCase();
  const shown = useMemo(() => (term ? data.filter((m) => m.name.toLowerCase().includes(term)) : data), [data, term]);
  const waiting = useMemo(() => shown.filter((m) => m.status === "Pending"), [shown]);
  const people = useMemo(() => shown.filter((m) => m.status !== "Pending"), [shown]);
  const totalWaiting = useMemo(() => data.filter((m) => m.status === "Pending").length, [data]);

  return (
    <Panel id="mg-members" title="Members" summary={data.length > 0 ? `${data.length} ${data.length === 1 ? "member" : "members"}${totalWaiting > 0 ? ` · ${totalWaiting} waiting` : ""}` : undefined}>
      {data.length > 8 && (
        <div className="relative mt-1">
          <Search size={14} className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-muted-foreground" />
          <Input aria-label="Find a member" placeholder="Find a member" className="pl-9" value={find} onChange={(e) => setFind(e.target.value)} />
        </div>
      )}
      <ListState loading={isLoading} error={isError} onRetry={() => refetch()} empty={data.length === 0} emptyText="Nobody is in this group yet.">
        {term && shown.length === 0 && <p className="mt-3 text-[13.5px] text-muted-foreground">No one called &ldquo;{find.trim()}&rdquo; in this group.</p>}
        {waiting.length > 0 && (
          <div className="mt-3">
            <p className="text-[12px] font-semibold uppercase tracking-wide text-muted-foreground">Waiting ({waiting.length})</p>
            <ul className="divide-y divide-border">{waiting.map((m) => <MemberRow key={m.memberId} m={m} busy={busy === m.memberId} onDecide={onDecide} />)}</ul>
            {waiting.some((m) => !m.canDecline) && <p className="pb-1 text-[12.5px] text-muted-foreground">A registration you don&apos;t approve stays with the administrators; only they can decline it.</p>}
          </div>
        )}
        {people.length > 0 && (
          <div className={waiting.length > 0 ? "mt-4" : "mt-3"}>
            {waiting.length > 0 && <p className="text-[12px] font-semibold uppercase tracking-wide text-muted-foreground">In the group ({people.length})</p>}
            <ul className="divide-y divide-border">{people.map((m) => <MemberRow key={m.memberId} m={m} busy={false} onDecide={onDecide} />)}</ul>
            {data.length >= 200 && <p className="pt-2 text-[12.5px] text-muted-foreground">Showing the most recent 200.</p>}
          </div>
        )}
      </ListState>
    </Panel>
  );
}

/** What has already gone to this group, by any leader who may message it, so nobody sends the same news twice. */
function History({ group }: { group: ManageGroup }) {
  const { data = [], isLoading, isError } = useQuery(manageQueries.messages(group));
  if (isLoading || isError || data.length === 0) return null;
  return (
    <div className="mt-6 border-t border-border pt-4">
      <h3 className="text-[13px] font-semibold">Recently sent to this group</h3>
      <ul className="mt-1 divide-y divide-border">
        {data.map((m) => (
          <li key={m.id} className="py-3">
            <p className="whitespace-pre-wrap break-words text-[13.5px] leading-relaxed">{m.text}</p>
            <p className="mt-1 text-[12px] text-muted-foreground">
              {m.isMine ? "You" : m.senderName} · <time dateTime={m.sentAt} title={formatDateTime(m.sentAt)}>{formatDate(m.sentAt)}</time> · {m.recipients} {m.recipients === 1 ? "person" : "people"}
            </p>
          </li>
        ))}
      </ul>
    </div>
  );
}

export function Message({ group }: { group: ManageGroup }) {
  const { user } = useAuth();
  const qc = useQueryClient();
  const [text, setText] = useState("");
  const [email, setEmail] = useState(false);
  const [sms, setSms] = useState(false);
  const [reviewing, setReviewing] = useState(false);
  const [understood, setUnderstood] = useState(false);
  const [sent, setSent] = useState<number | null>(null);
  const channels = useQuery(manageQueries.channels(group));
  const info = channels.data;
  const who = [user?.firstName, user?.lastName].filter(Boolean).join(" ") || "You";
  const extras: ("Email" | "Sms")[] = [...(email && info?.email ? ["Email" as const] : []), ...(sms && info?.sms ? ["Sms" as const] : [])];

  const send = useMutation({
    mutationFn: () => messageGroup(group, text.trim(), extras),
    onSuccess: (r) => {
      setSent(r.recipients); setReviewing(false); setUnderstood(false); setText(""); setEmail(false); setSms(false);
      toast.success(`Sent to ${r.recipients} ${r.recipients === 1 ? "person" : "people"}`);
      qc.invalidateQueries({ queryKey: manageQueries.channels(group).queryKey });
      qc.invalidateQueries({ queryKey: manageQueries.messages(group).queryKey });
    },
  });

  const people = info?.recipients ?? 0;
  const perDay = info?.perDay ?? 3;
  const left = info?.leftToday ?? perDay;
  const blocked = info ? (people === 0 ? "Nobody else is in this group yet." : left === 0 ? `You've sent today's ${perDay} ${perDay === 1 ? "message" : "messages"} to this group. You can send more tomorrow.` : null) : null;
  const open = () => { send.reset(); setUnderstood(false); setReviewing(true); };

  return (
    <Panel id="mg-message" title="Message the group">
      <p className="mt-1 text-[13px] leading-relaxed text-muted-foreground">Everyone in the group gets it as a notification and a phone alert, with your name on it.</p>
      <form className="mt-3 space-y-3" onSubmit={(e) => { e.preventDefault(); if (text.trim() && !blocked) open(); }}>
        <Textarea aria-label="Your message" rows={4} maxLength={500} placeholder="What would you like the group to know?" value={text} onChange={(e) => { setText(e.target.value); setSent(null); }} />
        {(info?.email || info?.sms) && (
          <fieldset className="space-y-2">
            <legend className="mb-1 text-[13px] font-semibold">Also send it by</legend>
            {info.email && <CheckOption id="msg-email" label="Email" hint="Each member with an email address on file." checked={email} onChange={setEmail} />}
            {info.sms && <CheckOption id="msg-sms" label="Text message" hint="Each member with a phone number on file." checked={sms} onChange={setSms} />}
          </fieldset>
        )}
        {blocked && <p role="status" className="bg-muted/60 px-3.5 py-3 text-[13px] leading-snug text-muted-foreground">{blocked}</p>}
        {sent !== null && !text && <p role="status" className="bg-primary/10 px-3.5 py-3 text-[13px] font-medium leading-snug text-primary">Sent to {sent} {sent === 1 ? "person" : "people"}. It can&apos;t be edited or removed.</p>}
        <div className="flex items-center justify-between gap-3">
          <span className="min-w-0 text-[12.5px] leading-snug text-muted-foreground"><span className="tabular-nums">{text.length}/500</span>{info && !blocked ? <> · goes to {people} {people === 1 ? "person" : "people"} · {left} of {perDay} left today</> : null}</span>
          <Button type="submit" disabled={!text.trim() || !!blocked || !info}>Review and send</Button>
        </div>
      </form>

      <History group={group} />

      <FormSheet open={reviewing} onClose={() => setReviewing(false)} noDiscardPrompt title="Check before you send" description={`To ${group.name}`}
        formId="manage-message-review" submitLabel={`Send to ${people} ${people === 1 ? "person" : "people"}`} savingLabel="Sending" cancelLabel="Go back and edit"
        saving={send.isPending} submitDisabled={!understood} error={errorText(send.error)}>
        <form id="manage-message-review" className="space-y-4" onSubmit={(e) => { e.preventDefault(); if (understood) send.mutate(); }}>
          {/* What they will see, and who: short enough that the warning below is on the first screen of a phone. */}
          <div className="border border-border bg-muted/40 p-4">
            <p className="text-[12px] font-semibold uppercase tracking-wide text-muted-foreground">{group.name}</p>
            <p className="mt-1.5 line-clamp-4 whitespace-pre-wrap break-words text-[14.5px] leading-relaxed"><span className="font-semibold">{who}:</span> {text.trim()}</p>
          </div>
          <div>
            <p className="text-[14px] font-semibold">To {people} {people === 1 ? "person" : "people"}</p>
            <p className="mt-0.5 text-[13px] leading-snug text-muted-foreground">
              Notification and phone alert{extras.includes("Email") ? ", email" : ""}{extras.includes("Sms") ? ", text message" : ""}. This uses 1 of your {left} {left === 1 ? "message" : "messages"} left today.
            </p>
          </div>
          <div role="alert" className="border border-amber-500/60 bg-amber-500/10 p-4">
            <p className="text-[14.5px] font-bold">This can&apos;t be undone</p>
            <p className="mt-1 text-[13.5px] leading-relaxed">Once you send it, it can&apos;t be edited, taken back or deleted. Everyone it reaches will see it, and it stays in their notifications.</p>
          </div>
          <CheckOption id="msg-understood" checked={understood} onChange={setUnderstood} label="I understand this message can't be removed once it's sent" />
        </form>
      </FormSheet>
    </Panel>
  );
}
