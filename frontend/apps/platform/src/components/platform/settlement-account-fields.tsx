"use client";

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Label } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { FormSelect } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Loader2, CheckCircle2, AlertCircle } from "lucide-react";
import { getBanks, resolveAccount } from "@/lib/platform-api";

export interface SettlementAccountValue {
  settlementBankCode: string;
  settlementBankName: string;
  settlementAccountNumber: string;
  settlementAccountName: string;
}

/**
 * Bank/mobile-money picker + account-number resolver, backed by Paystack's
 * own List Banks and Resolve Account Number endpoints — the bank name and
 * account holder name are both looked up from Paystack directly (read-only),
 * never typed by hand, since CreateSubaccountAsync needs the real bank code
 * and a wrong hand-typed one would silently misroute settlement money.
 */
export function SettlementAccountFields({
  value,
  onChange,
}: {
  value: SettlementAccountValue;
  onChange: (next: SettlementAccountValue) => void;
}) {
  const [channel, setChannel] = useState<"ghipss" | "mobile_money">("ghipss");
  const [resolveState, setResolveState] = useState<"idle" | "loading" | "resolved" | "error">(
    value.settlementAccountName ? "resolved" : "idle"
  );
  const [resolveError, setResolveError] = useState("");

  const { data: banks = [], isLoading: banksLoading } = useQuery({
    queryKey: ["banks", channel],
    queryFn: () => getBanks(channel),
  });

  const resolve = async (accountNumber: string, bankCode: string) => {
    if (!accountNumber || !bankCode) return;
    setResolveState("loading");
    setResolveError("");
    try {
      const result = await resolveAccount(accountNumber, bankCode);
      onChange({ ...value, settlementAccountNumber: accountNumber, settlementBankCode: bankCode, settlementAccountName: result.accountName });
      setResolveState("resolved");
    } catch {
      onChange({ ...value, settlementAccountNumber: accountNumber, settlementBankCode: bankCode, settlementAccountName: "" });
      setResolveState("error");
      setResolveError("Couldn't verify this account — check the account number and bank, then try again.");
    }
  };

  // Debounced auto-resolve once both a bank and a full-looking account number are present.
  useEffect(() => {
    if (!value.settlementBankCode || value.settlementAccountNumber.length < 9) return;
    const id = setTimeout(() => resolve(value.settlementAccountNumber, value.settlementBankCode), 600);
    return () => clearTimeout(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value.settlementAccountNumber, value.settlementBankCode]);

  return (
    <div className="space-y-4">
      <div className="flex gap-2">
        <Button
          type="button"
          size="sm"
          variant={channel === "ghipss" ? "default" : "outline"}
          onClick={() => setChannel("ghipss")}
        >
          Bank
        </Button>
        <Button
          type="button"
          size="sm"
          variant={channel === "mobile_money" ? "default" : "outline"}
          onClick={() => setChannel("mobile_money")}
        >
          Mobile money
        </Button>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div className="space-y-1.5">
          <Label>{channel === "ghipss" ? "Bank" : "Mobile money provider"}</Label>
          <FormSelect
            value={value.settlementBankCode}
            onValueChange={(code) => {
              const bank = banks.find((b) => b.code === code);
              onChange({ ...value, settlementBankCode: code, settlementBankName: bank?.name ?? "", settlementAccountName: "" });
              setResolveState("idle");
            }}
            placeholder={banksLoading ? "Loading…" : "Select…"}
            options={banks.map((b) => ({ value: b.code, label: b.name }))}
            disabled={banksLoading}
            className="w-full max-w-none"
          />
        </div>

        <div className="space-y-1.5">
          <Label>{channel === "ghipss" ? "Account number" : "Mobile money number"}</Label>
          <Input
            value={value.settlementAccountNumber}
            onChange={(e) => {
              onChange({ ...value, settlementAccountNumber: e.target.value.replace(/\D/g, ""), settlementAccountName: "" });
              setResolveState("idle");
            }}
            placeholder="1234567890"
            inputMode="numeric"
          />
        </div>
      </div>

      <div className="space-y-1.5">
        <Label>Account holder name</Label>
        <div className="flex items-center gap-2 h-11 px-3 rounded-[10px] border border-input bg-muted/40 text-[14px]">
          {resolveState === "loading" && (
            <span className="flex items-center gap-2 text-muted-foreground"><Loader2 size={14} className="animate-spin" /> Verifying with {channel === "ghipss" ? "bank" : "provider"}…</span>
          )}
          {resolveState === "resolved" && (
            <span className="flex items-center gap-2 text-success"><CheckCircle2 size={14} /> {value.settlementAccountName}</span>
          )}
          {resolveState === "error" && (
            <span className="flex items-center gap-2 text-destructive text-[13px]"><AlertCircle size={14} /> {resolveError}</span>
          )}
          {resolveState === "idle" && (
            <span className="text-muted-foreground">Select a {channel === "ghipss" ? "bank" : "provider"} and enter the account number to verify</span>
          )}
        </div>
        <p className="text-[11.5px] text-muted-foreground">Looked up automatically from {channel === "ghipss" ? "the bank" : "the provider"} — never typed by hand, so settlement funds always go to the right account.</p>
      </div>
    </div>
  );
}
