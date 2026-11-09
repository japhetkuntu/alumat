"use client";

import Link from "next/link";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { ConfirmModal } from "@alumni/ui";
import { useAuth } from "@/hooks/use-auth";
import { getMyRoles, stepDownFromRole, type MyRole } from "@/lib/member-api";
import { errorText } from "@/components/member/manage-ui";
import { groupHref } from "@/components/member/manage-parts";

/**
 * The roles this member holds and, in plain words, what each lets them do. Nothing at all for a member who holds none, so most people
 * never see it. It is the one place a leader can look to know exactly what they have been given.
 */
export function YourRoles() {
  const { isMember } = useAuth();
  const qc = useQueryClient();
  const { data: roles = [] } = useQuery({ queryKey: ["my-roles"], queryFn: getMyRoles, enabled: isMember, staleTime: 2 * 60_000, retry: false });
  const [leaving, setLeaving] = useState<MyRole | null>(null);
  const stepDown = useMutation({
    mutationFn: (r: MyRole) => stepDownFromRole(r.postId),
    onSuccess: (_v, r) => {
      toast.success(`You've stepped down as ${r.roleName}`);
      setLeaving(null);
      qc.invalidateQueries({ queryKey: ["my-roles"] });
      qc.invalidateQueries({ queryKey: ["manage-groups"] });
    },
    onError: (e) => { setLeaving(null); toast.error(errorText(e) ?? "That didn't work. Try again."); },
  });
  if (roles.length === 0) return null;

  return (
    <section aria-labelledby="your-roles" className="rounded-[18px] border border-border bg-background p-4 sm:p-5">
      <h2 id="your-roles" className="text-[15px] font-semibold">Your {roles.length === 1 ? "role" : "roles"}</h2>
      <ul className="mt-3 divide-y divide-border">
        {roles.map((r) => (
          <li key={r.postId} className="py-3 first:pt-0 last:pb-0">
            <p className="text-[14.5px] font-semibold leading-tight">
              {r.roleName}
              <span className="font-normal text-muted-foreground"> · {r.scopeType === "Institution" ? "the whole community" : r.scopeName}</span>
            </p>
            {r.privileges.length === 0 ? (
              <p className="mt-1 text-[13px] text-muted-foreground">Shown on your profile. It doesn&apos;t change what you can access.</p>
            ) : (
              <>
                <p className="mt-1 text-[13px] text-muted-foreground">{r.scopeType === "Institution" ? "In any group, you" : "In this group, you"} can:</p>
                <ul className="mt-1.5 space-y-1">
                  {r.privileges.map((p) => (
                    <li key={p.key} className="text-[13.5px] leading-snug"><span className="font-medium">{p.label}.</span> <span className="text-muted-foreground">{p.description}</span></li>
                  ))}
                </ul>
              </>
            )}
            <div className="mt-1 flex flex-wrap items-center gap-x-5">
              {r.privileges.length > 0 && (
                <Link href={r.scopeType === "Institution" ? "/manage" : groupHref({ scopeType: r.scopeType, scopeId: r.scopeId })}
                  className="inline-flex min-h-10 items-center text-[13px] font-semibold text-primary underline-offset-2 hover:underline">Open {r.scopeType === "Institution" ? "Manage" : r.scopeName}</Link>
              )}
              <button type="button" onClick={() => setLeaving(r)}
                className="inline-flex min-h-10 items-center text-[13px] text-muted-foreground underline-offset-2 hover:text-foreground hover:underline">Step down from this role</button>
            </div>
          </li>
        ))}
      </ul>

      <ConfirmModal
        open={!!leaving}
        title={leaving ? `Step down as ${leaving.roleName}?` : ""}
        message={`You lose what this role lets you do${leaving && leaving.scopeType !== "Institution" ? ` in ${leaving.scopeName}` : ""}, and the title leaves your profile. You stay a member. The administrators can see that you stepped down, and can appoint you again.`}
        confirmLabel="Step down"
        emphasis="cancel"
        isLoading={stepDown.isPending}
        onConfirm={() => leaving && stepDown.mutate(leaving)}
        onCancel={() => setLeaving(null)}
      />
    </section>
  );
}
