"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, FileText, CheckCircle2, ShieldCheck, Info } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { FormSelect } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Skeleton } from "@alumni/ui";
import { formatCurrency } from "@alumni/ui";
import { getServiceType, createServiceRequest } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { useNavTheme } from "@/components/member/member-layout";
import { toast } from "sonner";
import Link from "next/link";

export default function ServiceDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const qc = useQueryClient();
  const { data: navTheme } = useNavTheme();
  const institutionName = navTheme?.displayName || "your institution";
  const { data: service, isLoading } = useQuery({
    queryKey: ["service-type", params.id],
    queryFn: () => getServiceType(params.id),
  });

  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [files, setFiles] = useState<Record<string, File>>({});

  const submitMutation = useMutation({
    mutationFn: () => {
      const callbackUrl = `${window.location.origin}/services/callback`;
      return createServiceRequest(params.id, answers, files, callbackUrl);
    },
    onSuccess: (result) => {
      if (result.authorizationUrl) {
        setTimeout(() => { window.location.href = result.authorizationUrl!; }, 300);
      } else {
        toast.success("Request submitted");
        qc.invalidateQueries({ queryKey: ["my-service-requests"] });
        router.push("/services/requests");
      }
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  function submit() {
    if (!service) return;
    for (const field of service.fields) {
      if (field.type === "File") {
        if (field.required && !files[field.key]) {
          toast.error(`"${field.label}" is required.`);
          return;
        }
      } else if (field.required && !answers[field.key]?.trim()) {
        toast.error(`"${field.label}" is required.`);
        return;
      }
    }
    submitMutation.mutate();
  }

  if (isLoading) {
    return (
      <div className="p-4 sm:p-6 lg:p-8 max-w-[1200px] mx-auto space-y-6">
        <Skeleton className="h-4 w-28" />
        <div className="grid grid-cols-1 lg:grid-cols-[1fr_420px] gap-8">
          <div className="space-y-4">
            <Skeleton className="h-11 w-11 rounded-xl" />
            <Skeleton className="h-7 w-2/3" />
            <Skeleton className="h-20 w-full" />
          </div>
          <Skeleton className="h-96 w-full rounded-xl" />
        </div>
      </div>
    );
  }

  if (!service) {
    return (
      <div className="p-4 sm:p-6 lg:p-8 max-w-[1200px] mx-auto text-center py-20">
        <FileText size={28} className="mx-auto text-muted-foreground mb-3" />
        <p className="text-[15px] font-medium">Service not found</p>
        <p className="text-[13px] text-muted-foreground mt-1">It may have been removed or renamed.</p>
        <Link href="/services"><Button variant="outline" className="mt-4">Back to services</Button></Link>
      </div>
    );
  }

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1200px] mx-auto space-y-6">
      <Link href="/services" className="inline-flex items-center gap-1.5 text-[13px] text-muted-foreground hover:text-foreground w-fit">
        <ArrowLeft size={14} /> All services
      </Link>

      <div className="grid grid-cols-1 lg:grid-cols-[1fr_420px] gap-8 lg:gap-10 items-start">
        {/* ── About this service ── */}
        <div className="space-y-8">
          <div className="space-y-3">
            <div className="w-12 h-12 rounded-xl flex items-center justify-center bg-accent/10">
              <FileText size={22} className="text-accent" />
            </div>
            <h1 className="text-[26px] sm:text-[30px] font-bold leading-tight text-balance">{service.name}</h1>
            {service.description && (
              <p className="text-[15px] text-muted-foreground leading-relaxed max-w-[560px]">{service.description}</p>
            )}
          </div>

          {service.fields.length > 0 && (
            <div className="space-y-3">
              <p className="text-[12.5px] font-semibold uppercase tracking-wide text-muted-foreground">What you'll need</p>
              <ul className="space-y-2">
                {service.fields.map((field) => (
                  <li key={field.key} className="flex items-start gap-2.5 text-[14px]">
                    <CheckCircle2 size={16} className="text-accent shrink-0 mt-0.5" />
                    <span>
                      {field.label}
                      {!field.required && <span className="text-muted-foreground"> (optional)</span>}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {service.stages.length > 0 && (
            <div className="space-y-3">
              <p className="text-[12.5px] font-semibold uppercase tracking-wide text-muted-foreground">What happens after you submit</p>
              <ol className="relative border-l border-border/60 ml-1.5 space-y-5 pl-6">
                {service.stages.map((stage, i) => (
                  <li key={stage} className="relative">
                    <span className="absolute -left-[29px] top-0.5 w-3.5 h-3.5 rounded-full bg-accent/15 border-2 border-accent" />
                    <p className="text-[14px] font-medium leading-tight">{stage}</p>
                    {i === 0 && <p className="text-[12.5px] text-muted-foreground mt-0.5">Where your request starts, right after payment.</p>}
                  </li>
                ))}
              </ol>
            </div>
          )}

          <div className="space-y-2.5 max-w-[480px]">
            <div className="flex items-start gap-2.5 text-[12.5px] text-muted-foreground">
              <ShieldCheck size={15} className="shrink-0 mt-0.5" />
              <p>Payment is processed securely. You&apos;ll be able to track this request&apos;s progress from &quot;My requests&quot; once it&apos;s submitted.</p>
            </div>
            <div className="flex items-start gap-2.5 text-[12.5px] text-muted-foreground">
              <Info size={15} className="shrink-0 mt-0.5" />
              <p>{institutionName} handles this request directly — reviewing, processing, and fulfilling it themselves. Reach out to them for any questions about status or timing.</p>
            </div>
          </div>
        </div>

        {/* ── Request form ── */}
        <Card className="lg:sticky lg:top-6 border-border/60">
          <CardContent className="p-6 space-y-5">
            <div className="flex items-center justify-between pb-4 border-b border-border/60">
              <span className="text-[13px] font-medium text-muted-foreground">Total due</span>
              <span className="text-[22px] font-bold">{formatCurrency(service.price)}</span>
            </div>

            {service.fields.length === 0 ? (
              <p className="text-[13px] text-muted-foreground">No additional information needed — just submit your request.</p>
            ) : (
              <div className="space-y-4">
                {service.fields.map((field) => (
                  <div key={field.key} className="space-y-1.5">
                    <Label>
                      {field.label}{" "}
                      {!field.required && <span className="font-normal text-muted-foreground">(optional)</span>}
                    </Label>
                    {field.helpText && <p className="text-[12px] text-muted-foreground -mt-1">{field.helpText}</p>}

                    {field.type === "TextArea" ? (
                      <Textarea rows={3} value={answers[field.key] ?? ""} onChange={(e) => setAnswers((a) => ({ ...a, [field.key]: e.target.value }))} />
                    ) : field.type === "Select" ? (
                      <FormSelect
                        value={answers[field.key] ?? ""}
                        onValueChange={(v) => setAnswers((a) => ({ ...a, [field.key]: v }))}
                        placeholder="Select"
                        options={(field.options ?? []).map((o) => ({ value: o, label: o }))}
                      />
                    ) : field.type === "File" ? (
                      <input
                        type="file"
                        className="text-[13px]"
                        onChange={(e) => setFiles((f) => ({ ...f, [field.key]: e.target.files?.[0] as File }))}
                      />
                    ) : (
                      <Input
                        type={field.type === "Number" ? "number" : field.type === "Date" ? "date" : "text"}
                        value={answers[field.key] ?? ""}
                        onChange={(e) => setAnswers((a) => ({ ...a, [field.key]: e.target.value }))}
                      />
                    )}
                  </div>
                ))}
              </div>
            )}

            <Button className="w-full font-semibold" size="lg" isLoading={submitMutation.isPending} onClick={submit}>
              Pay {formatCurrency(service.price)} & submit
            </Button>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
