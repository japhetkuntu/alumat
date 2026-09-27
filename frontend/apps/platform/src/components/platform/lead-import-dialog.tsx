"use client";

import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  Button,
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  Label,
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
  Upload,
} from "@alumni/ui";
import { handleApiError } from "@/lib/api-client";
import { downloadCsv, toCsv } from "@/lib/csv";
import { parseLeadCsv, type ParsedLeadImport } from "@/lib/leads";
import { importOnboardingLeads, type ImportOnboardingLeadsResult } from "@/lib/platform-api";
import { LEAD_SOURCE_OPTIONS } from "./lead-edit-dialog";

const TEMPLATE = toCsv([
  ["Institution", "Contact", "Phone", "Email", "Role", "Source", "Stage", "Follow up", "Notes"],
  ["Example Old Students Association", "Ama Mensah", "0240000000", "", "General Secretary", "Warm intro", "Contacted", "2026-10-06", "Introduced by the UMaT OSA president"],
]);

/** Upload an outreach target list as CSV; the server skips duplicates and reports each skipped row. */
export function LeadImportDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const queryClient = useQueryClient();
  const [fileName, setFileName] = useState<string | null>(null);
  const [fileText, setFileText] = useState<string | null>(null);
  const [source, setSource] = useState("Outreach");
  const [result, setResult] = useState<ImportOnboardingLeadsResult | null>(null);

  const parsed: ParsedLeadImport | null = fileText === null ? null : parseLeadCsv(fileText, source);

  const mutation = useMutation({
    mutationFn: () => importOnboardingLeads(parsed!.rows),
    onSuccess: (data) => {
      setResult(data);
      toast.success(`Imported ${data.created} lead${data.created === 1 ? "" : "s"}`);
      queryClient.invalidateQueries({ queryKey: ["onboarding-leads"] });
      queryClient.invalidateQueries({ queryKey: ["activation-funnel"] });
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  function reset() {
    setFileName(null);
    setFileText(null);
    setResult(null);
  }

  async function onFile(file: File | undefined) {
    if (!file) return;
    setResult(null);
    setFileName(file.name);
    setFileText(await file.text());
  }

  return (
    <Dialog open={open} onOpenChange={(o) => { onOpenChange(o); if (!o) reset(); }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Import leads</DialogTitle>
        </DialogHeader>
        <div className="grid gap-4 mt-2">
          <p className="text-[13px] text-muted-foreground">
            Upload a CSV with one institution per row. An <b>Institution</b> and <b>Contact</b> column are required; Phone, Email, Role, Source, Stage, Follow up and Notes are optional. Institutions already in the pipeline are skipped.{" "}
            <button type="button" className="text-accent font-semibold hover:underline" onClick={() => downloadCsv("leads-template.csv", TEMPLATE)}>
              Download a template
            </button>
          </p>

          <div className="grid grid-cols-1 sm:grid-cols-[1fr_auto] gap-3 items-end">
            <label className="flex items-center gap-2 border border-dashed border-border px-4 py-3 cursor-pointer hover:bg-muted/40 text-[13px]">
              <Upload size={14} />
              <span className="truncate">{fileName ?? "Choose a .csv file"}</span>
              <input type="file" accept=".csv,text/csv" className="sr-only" onChange={(e) => onFile(e.target.files?.[0])} />
            </label>
            <div className="grid gap-1.5 min-w-[160px]">
              <Label>Source for rows without one</Label>
              <Select value={source} onValueChange={setSource}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {LEAD_SOURCE_OPTIONS.filter((o) => o !== "Website").map((o) => <SelectItem key={o} value={o}>{o}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
          </div>

          {parsed?.error && <p className="text-[13px] text-destructive">{parsed.error}</p>}
          {parsed && !parsed.error && !result && (
            <p className="text-[13px]">
              {parsed.rows.length} row{parsed.rows.length === 1 ? "" : "s"} ready to import.
              {parsed.ignoredHeaders.length > 0 && (
                <span className="text-muted-foreground"> Ignored columns: {parsed.ignoredHeaders.join(", ")}.</span>
              )}
            </p>
          )}

          {result && (
            <div className="border border-border">
              <p className="px-4 py-2.5 text-[13px] font-semibold border-b border-border">
                {result.created} imported · {result.skipped.length} skipped
              </p>
              {result.skipped.length > 0 && (
                <ul className="max-h-[200px] overflow-y-auto">
                  {result.skipped.map((s) => (
                    <li key={s.row} className="px-4 py-2 text-[12.5px] border-b border-border last:border-0">
                      Row {s.row}{s.institutionName ? ` (${s.institutionName})` : ""}: {s.reason}
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>{result ? "Done" : "Cancel"}</Button>
          {!result && (
            <Button
              onClick={() => mutation.mutate()}
              disabled={!parsed || !!parsed.error || parsed.rows.length === 0 || mutation.isPending}
            >
              {mutation.isPending ? "Importing…" : `Import ${parsed?.rows.length ?? 0} leads`}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
