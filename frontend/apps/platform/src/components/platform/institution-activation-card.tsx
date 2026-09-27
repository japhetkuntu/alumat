"use client";

import { useState } from "react";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Badge, Button, Card, CardContent, Check, Input, Label, LoadError, Skeleton, X, formatDate } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { handleApiError } from "@/lib/api-client";
import { activationStatus, trialLabel } from "@/lib/activation";
import { getInstitutionActivation, updateInstitutionActivationSettings } from "@/lib/platform-api";

const DEFAULT_MIN_MEMBERS = 100;

/** One institution's activation progress, with the per-institution member threshold and reminder switch. */
export function InstitutionActivationCard({ institutionId }: { institutionId: string }) {
  const { user } = useAuth();
  const canEdit = user?.role === "SuperAdmin" || user?.role === "Sales";
  const canView = !!user && user.role !== "Billing";
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: ["institution-activation", institutionId],
    queryFn: () => getInstitutionActivation(institutionId),
    enabled: canView,
  });
  // null = untouched, so the field shows the saved value until staff start editing.
  const [minMembersDraft, setMinMembersDraft] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: (vars: { activationMinMembers: number | null; setupNudgesEnabled: boolean }) =>
      updateInstitutionActivationSettings(institutionId, vars),
    onSuccess: (data) => {
      queryClient.setQueryData(["institution-activation", institutionId], data);
      setMinMembersDraft(null);
      queryClient.invalidateQueries({ queryKey: ["activation-scorecard"] });
      toast.success("Activation settings saved");
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  if (!canView) return null;
  const item = query.data;
  const status = item ? activationStatus(item) : null;
  const trial = item ? trialLabel(item.trialEndsAt, item.isActivated) : null;
  const savedMin = item && item.minMembers !== DEFAULT_MIN_MEMBERS ? String(item.minMembers) : "";
  const minMembers = minMembersDraft ?? savedMin;
  const draftMin = minMembers.trim() === "" ? null : Number(minMembers);
  const minChanged = item && (draftMin ?? DEFAULT_MIN_MEMBERS) !== item.minMembers;

  return (
    <Card className="lg:col-span-2">
      <div className="px-5 py-4 border-b border-border flex items-center justify-between gap-3">
        <div>
          <p className="text-[14px] font-semibold">Activation</p>
          {item && (
            <p className="text-[12px] text-muted-foreground mt-0.5">
              {item.isActivated && item.activatedAt ? `Activated ${formatDate(item.activatedAt)}` : `Day ${item.daysLive} of 30 · ${item.metCount} of 5 met`}
              {trial && <> · {trial}</>}
            </p>
          )}
        </div>
        <div className="flex items-center gap-2">
          {status && (
            <Badge variant={status === "Activated" ? "success" : status === "Overdue" ? "destructive" : status === "Stalled" ? "warning" : "neutral"}>
              {status}
            </Badge>
          )}
          <Link href="/activation" className="text-[12px] font-semibold text-accent hover:underline">All institutions</Link>
        </div>
      </div>
      <CardContent className="p-5">
        {query.isError && <LoadError onRetry={() => query.refetch()} />}
        {query.isLoading && <Skeleton className="h-32 w-full" />}
        {item && (
          <>
            <ul className="grid grid-cols-1 md:grid-cols-5 gap-3">
              {item.criteria.map((c) => (
                <li key={c.key} className="border border-border p-3">
                  <div className="flex items-center gap-1.5 text-[13px] font-semibold">
                    {c.met ? (
                      <Check size={13} style={{ color: "var(--success)" }} aria-label="Met" />
                    ) : (
                      <X size={13} className="text-muted-foreground" aria-label="Not met" />
                    )}
                    {c.label}
                  </div>
                  <p className="text-[12.5px] text-muted-foreground mt-1 leading-snug">{c.detail}</p>
                </li>
              ))}
            </ul>
            {item.nextStep && (
              <p className="text-[13px] mt-4"><span className="font-semibold">Next step: </span>{item.nextStep}</p>
            )}

            {canEdit && (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-5 pt-4 mt-4 border-t border-border">
                <div>
                  <Label htmlFor="activation-min-members">Member threshold</Label>
                  <p className="text-[12px] text-muted-foreground mt-0.5 mb-2">
                    Members needed for the members check. Leave blank for the default of {DEFAULT_MIN_MEMBERS}; lower it for small associations and year groups.
                  </p>
                  <div className="flex gap-2">
                    <Input
                      id="activation-min-members"
                      type="number"
                      min={1}
                      placeholder={String(DEFAULT_MIN_MEMBERS)}
                      value={minMembers}
                      onChange={(e) => setMinMembersDraft(e.target.value)}
                      className="w-[140px]"
                    />
                    <Button
                      variant="outline"
                      disabled={!minChanged || (draftMin !== null && draftMin < 1) || mutation.isPending}
                      onClick={() => mutation.mutate({ activationMinMembers: draftMin, setupNudgesEnabled: item.setupNudgesEnabled })}
                    >
                      Save
                    </Button>
                  </div>
                </div>
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <p className="text-[13px] font-semibold">Setup reminders</p>
                    <p className="text-[12px] text-muted-foreground mt-0.5">
                      Weekly email (and SMS, when the institution has SMS on) to its SuperAdmins with their next setup step, for its first 90 days. Separate from member notifications.
                    </p>
                  </div>
                  <button
                    type="button"
                    role="switch"
                    aria-label="Setup reminders"
                    aria-checked={item.setupNudgesEnabled}
                    disabled={mutation.isPending}
                    onClick={() => mutation.mutate({ activationMinMembers: item.minMembers === DEFAULT_MIN_MEMBERS ? null : item.minMembers, setupNudgesEnabled: !item.setupNudgesEnabled })}
                    className={`relative inline-flex h-6 w-11 shrink-0 rounded-full border-2 border-transparent transition-colors disabled:opacity-60 ${item.setupNudgesEnabled ? "bg-primary" : "bg-muted"}`}
                  >
                    <span className={`pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow-lg transition-transform ${item.setupNudgesEnabled ? "translate-x-5" : "translate-x-0"}`} />
                  </button>
                </div>
              </div>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
