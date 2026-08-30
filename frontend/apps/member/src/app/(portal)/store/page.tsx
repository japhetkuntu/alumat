"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useQuery, useMutation } from "@tanstack/react-query";
import { ShoppingBag, ShoppingCart, Plus, Minus, X, Package, Receipt } from "lucide-react";
import { Button } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { formatCurrency } from "@alumni/ui";
import { getStoreProducts, checkoutStoreCart } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { useStoreCart, lineUnitPrice } from "@/hooks/use-store-cart";
import { toast } from "sonner";

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

  const { data, isLoading } = useQuery({
    queryKey: ["store-products"],
    queryFn: () => getStoreProducts(1, 50),
  });
  const products = data?.results ?? [];
  // Pass undefined (not []) while the query is still loading — the cart hook treats
  // an empty array as "confirmed no products exist" and reconciles cart lines
  // against it, which would wipe every line before the real product list ever loads.
  const liveProducts = isLoading ? undefined : products;

  const { cart, addToCart, updateQuantity, removeFromCart, cartTotal, cartCount } = useStoreCart(liveProducts);

  const checkoutMut = useMutation({
    mutationFn: () => {
      const callbackUrl = `${window.location.origin}/store/callback`;
      return checkoutStoreCart(
        cart.map((l) => ({ productId: l.productId, quantity: l.quantity, variantId: l.variantId })),
        callbackUrl
      );
    },
    onSuccess: (result) => {
      if (result.authorizationUrl) {
        setTimeout(() => { window.location.href = result.authorizationUrl!; }, 300);
      }
    },
    onError: (e) => toast.error(handleApiError(e)),
  });

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-6">
      <div className="flex items-start justify-between gap-4 flex-wrap">
        <PageHeader
          eyebrow="Store"
          title="Alumni store"
          description="Buy branded merchandise and support the association. Delivery details are shown per item."
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
                  const stock = l.variant ? l.variant.quantityAvailable : l.product.quantityAvailable;
                  const label = variantLabel(l.variant?.options);
                  const thumb = l.variant?.imageUrl || l.product.imageUrls?.[0];
                  const lineKey = `${l.productId}:${l.variantId ?? ""}`;
                  return (
                    <div key={lineKey} className="flex items-center gap-3">
                      <Link href={`/store/${l.productId}`} className="w-12 h-12 rounded-lg bg-muted/50 shrink-0 overflow-hidden flex items-center justify-center">
                        {thumb ? (
                          <img src={thumb} alt={l.product.name} className="w-full h-full object-cover" />
                        ) : (
                          <Package size={18} className="text-muted-foreground" />
                        )}
                      </Link>
                      <div className="flex-1 min-w-0">
                        <Link href={`/store/${l.productId}`} className="text-[13px] font-semibold truncate block hover:underline">{l.product.name}</Link>
                        {label && <p className="text-[11.5px] text-muted-foreground truncate">{label}</p>}
                        <p className="text-[12px] text-muted-foreground">{formatCurrency(unitPrice)} each</p>
                        {stock - l.quantity <= 2 && (
                          <p className="text-[11px] text-warning font-medium mt-0.5">Only {stock} left</p>
                        )}
                      </div>
                      <div className="flex items-center gap-1.5 shrink-0">
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
                  );
                })}
                <div className="flex items-center justify-between pt-3 border-t border-border flex-wrap gap-2">
                  <span className="text-[14px] font-bold">Total: {formatCurrency(cartTotal)}</span>
                  <Button
                    className="font-semibold gap-2"
                    onClick={() => checkoutMut.mutate()}
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
        <EmptyState icon={<ShoppingBag size={40} />} title="No products yet" description="Check back soon — the alumni store is empty for now." />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {products.map((p) => {
            const hasVariants = p.variantOptionTypes.length > 0;
            const linesForProduct = cart.filter((l) => l.productId === p.id);
            const inCartCount = linesForProduct.reduce((sum, l) => sum + l.quantity, 0);
            const soldOut = hasVariants
              ? p.variants.every((v) => v.quantityAvailable <= 0)
              : p.quantityAvailable <= 0;
            const priceLabel = hasVariants
              ? `From ${formatCurrency(Math.min(...p.variants.map((v) => v.price)))}`
              : formatCurrency(p.price);
            return (
              <Card key={p.id} className="flex flex-col overflow-hidden">
                <Link href={`/store/${p.id}`} className="block">
                  {p.imageUrls?.[0] ? (
                    <img src={p.imageUrls[0]} alt={p.name} className="w-full h-36 object-cover" />
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
                  ) : !hasVariants ? (
                    <p className="text-[11px] text-muted-foreground">{p.quantityAvailable} left</p>
                  ) : null}
                  {hasVariants ? (
                    <Link href={`/store/${p.id}`} className="mt-auto">
                      <Button size="sm" className="w-full text-[12px] font-bold gap-1.5" disabled={soldOut}>
                        <ShoppingCart size={13} />
                        {inCartCount > 0 ? `In cart (${inCartCount}) — choose options` : "Choose options"}
                      </Button>
                    </Link>
                  ) : (
                    <Button
                      size="sm"
                      className="mt-auto w-full text-[12px] font-bold gap-1.5"
                      disabled={soldOut}
                      onClick={() => addToCart(p)}
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
