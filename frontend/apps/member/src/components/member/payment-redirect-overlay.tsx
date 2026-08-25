"use client";

import { CreditCard, ShieldCheck } from "lucide-react";
import { formatCurrency } from "@alumni/ui";
import { GPU_LAYER_STYLE } from "@/lib/gpu-layer-style";

/**
 * Shown for the brief window between "Pay" being clicked and the browser
 * actually leaving for Paystack (the payment-initiation network round-trip,
 * then the hard navigation itself). Replaces what used to be a silent/abrupt
 * redirect with a clear, on-brand "here's what's happening" moment — not
 * dismissable, since a real navigation is either about to happen or the
 * mutation is about to fail (in which case `visible` flips false on its own
 * and the existing error toast takes over).
 */
export function PaymentRedirectOverlay({
  visible,
  campaignTitle,
  amount,
}: {
  visible: boolean;
  campaignTitle?: string;
  amount?: number;
}) {
  if (!visible) return null;

  return (
    <div
      className="fixed inset-0 z-[10001] flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm animate-in fade-in duration-200"
      style={GPU_LAYER_STYLE}
      role="status"
      aria-live="polite"
    >
      <div
        className="w-full max-w-[340px] rounded-[20px] border p-7 text-center shadow-[0_8px_32px_rgba(0,0,0,0.2)] animate-in zoom-in-95 duration-250"
        style={{ background: "var(--background)", borderColor: "var(--border)" }}
      >
        <div className="relative w-16 h-16 mx-auto mb-5">
          <div className="absolute inset-0 rounded-full animate-ping opacity-40" style={{ background: "var(--primary)" }} />
          <div
            className="relative w-16 h-16 rounded-full flex items-center justify-center"
            style={{ background: "var(--primary)" }}
          >
            <CreditCard size={26} className="text-white" />
          </div>
        </div>

        <p className="text-[16px] font-bold" style={{ color: "var(--foreground)" }}>
          Redirecting to secure payment
        </p>

        {(campaignTitle || amount) && (
          <p className="text-[13.5px] mt-1.5 leading-relaxed" style={{ color: "var(--muted-foreground)" }}>
            {amount ? formatCurrency(amount) : null}
            {amount && campaignTitle ? " for " : null}
            {campaignTitle ? <span className="font-semibold">{campaignTitle}</span> : null}
          </p>
        )}

        <p className="text-[12.5px] mt-4" style={{ color: "var(--muted-foreground)" }}>
          Please don&apos;t close this tab — you&apos;ll land on Paystack&apos;s secure checkout in a moment.
        </p>

        <div
          className="flex items-center justify-center gap-1.5 mt-5 pt-4 border-t"
          style={{ borderColor: "var(--border)" }}
        >
          <ShieldCheck size={13} style={{ color: "var(--muted-foreground)" }} />
          <span className="text-[11px] font-semibold uppercase tracking-wide" style={{ color: "var(--muted-foreground)" }}>
            Secured by Paystack
          </span>
        </div>
      </div>
    </div>
  );
}
