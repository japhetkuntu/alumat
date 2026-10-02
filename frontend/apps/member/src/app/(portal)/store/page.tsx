"use client";

import { useEffect, useRef, useState } from "react";
import { NotifyMeButton } from "@/components/member/notify-me-button";
import Link from "next/link";
import { useQuery, useMutation } from "@tanstack/react-query";
import { ShoppingBag, ShoppingCart, Plus, Minus, X, Package, Receipt } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { formatCurrency } from "@alumni/ui";
import { getStoreProducts, checkoutStoreCart } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { useStoreCart, lineUnitPrice } from "@/hooks/use-store-cart";
import { StoreItemForm, emptyItemForm, productNeedsForm, validateItemForm, type ItemFormState } from "@/components/member/store-item-form";
import { toast } from "sonner";
import { useNavTheme } from "@/components/member/member-layout";
import { FitImage } from "@alumni/ui";

function variantLabel(options?: Record<string, string>) {
  if (!options) return null;
  const values = Object.values(options).filter(Boolean);
  return values.length > 0 ? values.join(" / ") : null;
}

export default function StorePage() {
  const [showCart, setShowCart] = useState(false);
  const cartPanelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (showCart) {
      cartPanelRef.current?.scrollIntoView({ behavior: "smooth", block: "start" });
    }
  }, [showCart]);

  const { data, isLoading, isSuccess } = useQuery({
    queryKey: ["store-products"],
    queryFn: () => getStoreProducts(1, 50),
  });
  const products = data?.results ?? [];
  // Pass undefined (not []) until the list has actually loaded — the cart hook treats an empty array
  // as "confirmed no products exist" and reconciles cart lines against it, which would wipe every
  // line while loading, and also if the request fails.
  const liveProducts = isSuccess ? products : undefined;

  const { cart, addToCart, updateQuantity, removeFromCart, cartTotal, cartCount } = useStoreCart(liveProducts);
  const { data: navTheme } = useNavTheme();
  const isCommunity = navTheme?.organizationType === "Community";

  // Answers live in page state, not the persisted cart: File objects can't be serialised to localStorage.
  const [forms, setForms] = useState<Record<string, ItemFormState>>({});
  const lineKeyOf = (l: { productId: string; variantId?: string }) => `${l.productId}:${l.variantId ?? ""}`;

  const checkoutMut = useMutation({
    mutationFn: () => {
      const callbackUrl = `${window.location.origin}/store/callback`;
      const files: Record<string, File> = {};
      const items = cart.map((l, index) => {
        const form = forms[lineKeyOf(l)] ?? emptyItemForm;
        Object.entries(form.files).forEach(([k, file]) => {
          if (file) files[`${index}:${k}`] = file;
        });
        return {
          productId: l.productId,
          quantity: l.quantity,
          variantId: l.variantId,
          ...(productNeedsForm(l.product) ? { answers: form.answers, deliveryAnswers: form.delivery } : {}),
        };
      });
      return checkoutStoreCart(items, callbackUrl, files);
    },
    onSuccess: (result) => {
      if (result.authorizationUrl) {
        setTimeout(() => { window.location.href = result.authorizationUrl!; }, 300);
      }
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  function startCheckout() {
    for (const l of cart) {
      const problem = validateItemForm(l.product, forms[lineKeyOf(l)] ?? emptyItemForm);
      if (problem) {
        toast.error(problem);
        return;
      }
    }
    checkoutMut.mutate();
  }

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-6">
      <div className="flex items-start justify-between gap-4 flex-wrap">
        <PageHeader
          eyebrow="Store"
          title="Store"
          description="Browse what your institution offers. Anything an item needs from you is asked for in your cart."
        />
        <div className="flex items-center gap-2 shrink-0">
          <Link href="/store/orders">
            <Button variant="outline" className="gap-2">
              <Receipt size={16} />
              Order history
            </Button>
          </Link>
          <Button variant="outline" className="gap-2" onClick={() => setShowCart((v) => !v)}>
            <ShoppingCart size={16} />
            Cart{cartCount > 0 ? ` (${cartCount})` : ""}
          </Button>
        </div>
      </div>

      {showCart && (
        <Card className="border-primary/30" ref={cartPanelRef}>
          <CardContent className="p-4 space-y-3">
            {cart.length === 0 ? (
              <div className="flex flex-col items-center justify-center gap-3 py-8 text-center">
                <div className="w-12 h-12 rounded-full bg-muted/50 flex items-center justify-center">
                  <ShoppingCart size={20} className="text-muted-foreground" />
                </div>
                <div>
                  <p className="text-[13.5px] font-semibold">Your cart is empty</p>
                  <p className="text-[12px] text-muted-foreground mt-0.5">Add items from the store to get started.</p>
                </div>
                <Button variant="outline" size="sm" onClick={() => setShowCart(false)}>
                  Browse the store
                </Button>
              </div>
            ) : (
              <>
                {cart.map((l) => {
                  const unitPrice = lineUnitPrice(l.product, l.variant);
                  const tracked = l.product.trackStock !== false;
                  const stock = l.variant ? l.variant.quantityAvailable : l.product.quantityAvailable;
                  const label = variantLabel(l.variant?.options);
                  const thumb = l.variant?.imageUrl || l.product.imageUrls?.[0];
                  const lineKey = `${l.productId}:${l.variantId ?? ""}`;
                  return (
                    <div key={lineKey} className="space-y-3 pb-3 border-b border-border/60 last:border-b-0 last:pb-0">
                    <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
                      <Link href={`/store/${l.productId}`} className="w-12 h-12 rounded-lg bg-muted/50 shrink-0 overflow-hidden flex items-center justify-center">
                        {thumb ? (
                          <FitImage src={thumb} alt={l.product.name} className="w-full h-full" />
                        ) : (
                          <Package size={18} className="text-muted-foreground" />
                        )}
                      </Link>
                      <div className="flex-1 min-w-[9rem]">
                        <Link href={`/store/${l.productId}`} className="text-[13px] font-semibold block hover:underline">{l.product.name}</Link>
                        {label && <p className="text-[11.5px] text-muted-foreground break-words">{label}</p>}
                        <p className="text-[12px] text-muted-foreground">
                          {formatCurrency(unitPrice)}{l.product.priceLabel ? ` ${l.product.priceLabel}` : " each"}
                        </p>
                        {tracked && stock - l.quantity <= 2 && (
                          <p className="text-[11px] text-warning font-medium mt-0.5">Only {stock} left</p>
                        )}
                      </div>
                      <div className="flex items-center gap-1.5 shrink-0 max-sm:ml-[60px]">
                        <button className="w-9 h-9 rounded-md border border-border flex items-center justify-center hover:bg-muted" onClick={() => updateQuantity(l.productId, l.variantId, -1)}>
                          <Minus size={12} />
                        </button>
                        <span className="w-6 text-center text-[13px] font-semibold">{l.quantity}</span>
                        <button className="w-9 h-9 rounded-md border border-border flex items-center justify-center hover:bg-muted" onClick={() => updateQuantity(l.productId, l.variantId, 1)}>
                          <Plus size={12} />
                        </button>
                        <button className="w-9 h-9 rounded-md flex items-center justify-center text-destructive hover:bg-destructive/10 ml-1" onClick={() => removeFromCart(l.productId, l.variantId)}>
                          <X size={13} />
                        </button>
                      </div>
                    </div>
                    {productNeedsForm(l.product) && (
                      <div className="sm:pl-[60px]">
                        <StoreItemForm
                          product={l.product}
                          form={forms[lineKey] ?? emptyItemForm}
                          onChange={(next) => setForms((f) => ({ ...f, [lineKey]: next }))}
                        />
                      </div>
                    )}
                    </div>
                  );
                })}
                <div className="flex items-center justify-between pt-3 border-t border-border flex-wrap gap-2">
                  <span className="text-[14px] font-bold">Total: {formatCurrency(cartTotal)}</span>
                  <Button
                    className="font-semibold gap-2"
                    onClick={startCheckout}
                    isLoading={checkoutMut.isPending}
                    loadingText="Redirecting…"
                  >
                    Checkout
                  </Button>
                </div>
              </>
            )}
          </CardContent>
        </Card>
      )}

      {isLoading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {Array.from({ length: 8 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : products.length === 0 ? (
        <EmptyState icon={<ShoppingBag size={40} />} title="The community store" description="Your institution sells merchandise and other items here, and the money supports the community. No products have been added yet." action={<NotifyMeButton />} />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {products.map((p) => {
            const hasVariants = p.variantOptionTypes.length > 0;
            const linesForProduct = cart.filter((l) => l.productId === p.id);
            const inCartCount = linesForProduct.reduce((sum, l) => sum + l.quantity, 0);
            const tracked = p.trackStock !== false;
            const soldOut = tracked && (hasVariants
              ? p.variants.every((v) => v.quantityAvailable <= 0)
              : p.quantityAvailable <= 0);
            const priceLabel =
              (hasVariants
                ? `From ${formatCurrency(Math.min(...p.variants.map((v) => v.price)))}`
                : formatCurrency(p.price)) + (p.priceLabel ? ` ${p.priceLabel}` : "");
            return (
              <Card key={p.id} className="flex flex-col overflow-hidden">
                <Link href={`/store/${p.id}`} className="block">
                  {p.imageUrls?.[0] ? (
                    <FitImage src={p.imageUrls[0]} alt={p.name} className="w-full h-36" />
                  ) : (
                    <div className="w-full h-36 bg-muted/40 flex items-center justify-center">
                      <Package size={26} className="text-muted-foreground" />
                    </div>
                  )}
                </Link>
                <CardContent className="flex-1 flex flex-col p-3 space-y-1.5">
                  <Link href={`/store/${p.id}`} className="hover:underline">
                    <h3 className="text-[13px] font-semibold leading-snug line-clamp-2">{p.name}</h3>
                  </Link>
                  <p className="text-[14px] font-bold text-primary">{priceLabel}</p>
                  {soldOut ? (
                    <p className="text-[11px] text-destructive font-medium">Sold out</p>
                  ) : !hasVariants && tracked ? (
                    <p className="text-[11px] text-muted-foreground">{p.quantityAvailable} left</p>
                  ) : null}
                  {hasVariants ? (
                    <Link href={`/store/${p.id}`} className="mt-auto">
                      <Button size="sm" className="w-full text-[12px] font-bold gap-1.5" disabled={soldOut}>
                        <ShoppingCart size={13} />
                        {inCartCount > 0 ? `In cart (${inCartCount}), choose options` : "Choose options"}
                      </Button>
                    </Link>
                  ) : (
                    <Button
                      size="sm"
                      className="mt-auto w-full text-[12px] font-bold gap-1.5"
                      disabled={soldOut}
                      onClick={() => { addToCart(p); if (productNeedsForm(p)) setShowCart(true); }}
                    >
                      <ShoppingCart size={13} />
                      {inCartCount > 0 ? `In cart (${inCartCount})` : "Add to cart"}
                    </Button>
                  )}
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}
