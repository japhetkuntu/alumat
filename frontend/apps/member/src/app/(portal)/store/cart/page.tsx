"use client";

import { useRef, useState } from "react";
import Link from "next/link";
import { useMutation, useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { ArrowLeft, Minus, Package, Plus, ShoppingBag, ShoppingCart } from "@alumni/ui";
import { Button, EmptyState, FitImage, Skeleton, formatCurrency } from "@alumni/ui";
import { checkoutStoreCart, getStoreProducts } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { lineStock, lineUnitPrice, useCartSummary, useStoreCart, type CartLine } from "@/hooks/use-store-cart";
import {
  StoreItemForm, emptyItemForm, productNeedsForm, validateItemForm, type ItemFormState,
} from "@/components/member/store-item-form";

const lineKeyOf = (l: { productId: string; variantId?: string }) => `${l.productId}:${l.variantId ?? ""}`;

function variantLabel(options?: Record<string, string>) {
  const values = Object.values(options ?? {}).filter(Boolean);
  return values.length > 0 ? values.join(" / ") : null;
}

function Stepper({ line, onChange }: { line: CartLine; onChange: (delta: number) => void }) {
  const atMax = lineStock(line.product, line.variant) <= line.quantity;
  const btn = "flex h-10 w-10 items-center justify-center border border-border bg-background hover:bg-muted disabled:cursor-not-allowed disabled:opacity-40 focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary";
  return (
    <div className="inline-flex items-center">
      <button type="button" className={btn} aria-label={`Decrease quantity of ${line.product.name}`} onClick={() => onChange(-1)}>
        <Minus size={13} />
      </button>
      <span className="flex h-10 min-w-10 items-center justify-center border-y border-border px-2 text-[14px] font-semibold tabular-nums" aria-live="polite">
        {line.quantity}
      </span>
      <button type="button" className={btn} aria-label={`Increase quantity of ${line.product.name}`} disabled={atMax} onClick={() => onChange(1)}>
        <Plus size={13} />
      </button>
    </div>
  );
}

export default function StoreCartPage() {
  const { data, isSuccess } = useQuery({ queryKey: ["store-products"], queryFn: () => getStoreProducts(1, 50) });
  // Undefined until the list has loaded: an empty list means "no products exist" to the cart and would clear every line.
  const { cart, updateQuantity, removeFromCart, cartTotal, cartCount } = useStoreCart(isSuccess ? data?.results ?? [] : undefined);

  // Answers live in page state: File objects cannot be saved with the cart.
  const [forms, setForms] = useState<Record<string, ItemFormState>>({});
  const [problem, setProblem] = useState<{ key: string; message: string } | null>(null);
  const lineRefs = useRef<Record<string, HTMLLIElement | null>>({});
  // The cart is restored from storage after the first render, so tell "not loaded yet" from "empty".
  // The saved cart has something in it that this page has not loaded yet: show the skeleton instead of a flash of "empty".
  const savedCount = useCartSummary().count;
  const ready = !(cart.length === 0 && savedCount > 0);

  const checkout = useMutation({
    mutationFn: () => {
      const files: Record<string, File> = {};
      const items = cart.map((l, index) => {
        const form = forms[lineKeyOf(l)] ?? emptyItemForm;
        Object.entries(form.files).forEach(([k, file]) => { if (file) files[`${index}:${k}`] = file; });
        return {
          productId: l.productId,
          quantity: l.quantity,
          variantId: l.variantId,
          ...(productNeedsForm(l.product) ? { answers: form.answers, deliveryAnswers: form.delivery } : {}),
        };
      });
      return checkoutStoreCart(items, `${window.location.origin}/store/callback`, files);
    },
    onSuccess: (result) => {
      if (result.authorizationUrl) window.location.href = result.authorizationUrl;
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  function startCheckout() {
    for (const l of cart) {
      const message = validateItemForm(l.product, forms[lineKeyOf(l)] ?? emptyItemForm);
      if (message) {
        const key = lineKeyOf(l);
        setProblem({ key, message });
        lineRefs.current[key]?.scrollIntoView({ behavior: "smooth", block: "center" });
        return;
      }
    }
    setProblem(null);
    checkout.mutate();
  }

  const needsDetails = cart.filter((l) => productNeedsForm(l.product)).length;

  if (!ready) {
    return (
      <div className="mx-auto max-w-[1000px] space-y-4 p-4 sm:p-6 lg:p-8">
        <Skeleton className="h-8 w-40" />
        <Skeleton className="h-28 w-full" />
        <Skeleton className="h-28 w-full" />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-[1000px] space-y-6 p-4 pb-28 sm:p-6 lg:p-8 lg:pb-8">
      <div>
        <Link href="/store" className="inline-flex items-center gap-1.5 text-[13px] text-muted-foreground hover:text-foreground">
          <ArrowLeft size={14} /> Continue shopping
        </Link>
        <h1 className="mt-3 text-[22px] font-bold leading-tight sm:text-[26px]">
          Your cart{cartCount > 0 && <span className="ml-2 text-[15px] font-normal text-muted-foreground">{cartCount} {cartCount === 1 ? "item" : "items"}</span>}
        </h1>
      </div>

      {cart.length === 0 ? (
        <EmptyState
          icon={<ShoppingCart size={40} />}
          title="Your cart is empty"
          description="Items you add from the store wait here until you check out."
          action={<Link href="/store"><Button>Browse the store</Button></Link>}
        />
      ) : (
        <div className="grid gap-6 lg:grid-cols-[1fr_320px] lg:items-start">
          <ul className="divide-y divide-border border border-border bg-card">
            {cart.map((l) => {
              const key = lineKeyOf(l);
              const unit = lineUnitPrice(l.product, l.variant);
              const label = variantLabel(l.variant?.options);
              const thumb = l.variant?.imageUrl || l.product.imageUrls?.[0];
              const stock = lineStock(l.product, l.variant);
              const low = Number.isFinite(stock) && stock - l.quantity <= 2;
              return (
                <li key={key} ref={(el) => { lineRefs.current[key] = el; }} className="space-y-3 p-3.5 sm:p-4">
                  <div className="flex gap-3">
                    <Link href={`/store/${l.productId}`} className="flex size-16 shrink-0 items-center justify-center overflow-hidden bg-muted/50 sm:size-20">
                      {thumb ? <FitImage src={thumb} alt="" className="size-full" /> : <Package size={20} className="text-muted-foreground" />}
                    </Link>
                    <div className="min-w-0 flex-1">
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                          <Link href={`/store/${l.productId}`} className="block text-[14.5px] font-semibold leading-snug hover:underline">{l.product.name}</Link>
                          {label && <p className="text-[12.5px] text-muted-foreground">{label}</p>}
                          <p className="text-[12.5px] text-muted-foreground">
                            {formatCurrency(unit)}{l.product.priceLabel ? ` ${l.product.priceLabel}` : " each"}
                          </p>
                        </div>
                        <p className="shrink-0 text-[15px] font-bold tabular-nums">{formatCurrency(unit * l.quantity)}</p>
                      </div>
                      <div className="mt-2.5 flex flex-wrap items-center justify-between gap-2">
                        <Stepper line={l} onChange={(d) => updateQuantity(l.productId, l.variantId, d)} />
                        <button
                          type="button"
                          onClick={() => removeFromCart(l.productId, l.variantId)}
                          className="min-h-10 px-1 text-[13px] font-semibold text-destructive underline-offset-2 hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary"
                        >
                          Remove
                        </button>
                      </div>
                      {low && <p className="mt-1.5 text-[12.5px] font-medium text-warning">Only {stock} available</p>}
                    </div>
                  </div>

                  {productNeedsForm(l.product) && (
                    <div className="border-t border-border pt-3 sm:pl-[92px]">
                      <p className="mb-2 text-[13px] font-semibold">Details for this item</p>
                      <StoreItemForm
                        product={l.product}
                        form={forms[key] ?? emptyItemForm}
                        onChange={(next) => { setForms((f) => ({ ...f, [key]: next })); if (problem?.key === key) setProblem(null); }}
                      />
                      {problem?.key === key && <p role="alert" className="mt-2 text-[13px] font-medium text-destructive">{problem.message}</p>}
                    </div>
                  )}
                </li>
              );
            })}
          </ul>

          <aside aria-label="Order summary" className="space-y-3 border border-border bg-card p-4 lg:sticky lg:top-6">
            <h2 className="text-[15px] font-bold">Order summary</h2>
            <dl className="space-y-1.5 text-[13.5px]">
              <div className="flex justify-between gap-3">
                <dt className="text-muted-foreground">{cartCount} {cartCount === 1 ? "item" : "items"}</dt>
                <dd className="tabular-nums">{formatCurrency(cartTotal)}</dd>
              </div>
            </dl>
            <div className="flex items-baseline justify-between gap-3 border-t border-border pt-3">
              <span className="text-[14px] font-semibold">Total</span>
              <span className="text-[20px] font-bold tabular-nums">{formatCurrency(cartTotal)}</span>
            </div>
            {needsDetails > 0 && (
              <p className="text-[12.5px] text-muted-foreground">
                {needsDetails === 1 ? "One item needs" : `${needsDetails} items need`} a few details from you before you pay.
              </p>
            )}
            <Button className="hidden w-full font-semibold lg:inline-flex" size="lg" onClick={startCheckout} isLoading={checkout.isPending} loadingText="Taking you to payment…">
              Pay {formatCurrency(cartTotal)}
            </Button>
            <p className="hidden text-[12px] text-muted-foreground lg:block">You will be taken to a secure payment page, then back here.</p>
          </aside>
        </div>
      )}

      {cart.length > 0 && (
        <div
          className="fixed inset-x-0 z-30 border-t border-border bg-background py-2.5 pl-3 pr-[4.75rem] lg:hidden"
          style={{ bottom: "calc(4rem + env(safe-area-inset-bottom))" }}
        >
          <div className="mx-auto flex max-w-[1000px] items-center gap-3">
            <div className="min-w-0">
              <p className="text-[12px] text-muted-foreground">Total</p>
              <p className="text-[17px] font-bold leading-tight tabular-nums">{formatCurrency(cartTotal)}</p>
            </div>
            <Button className="ml-auto h-11 flex-1 font-semibold sm:max-w-[240px]" onClick={startCheckout} isLoading={checkout.isPending} loadingText="Taking you to payment…">
              Pay now
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
