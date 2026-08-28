"use client";

import { useState } from "react";
import Link from "next/link";
import { useQuery, useMutation } from "@tanstack/react-query";
import { ShoppingBag, ShoppingCart, Plus, Minus, X, Package } from "lucide-react";
import { Button } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { formatCurrency } from "@alumni/ui";
import { getStoreProducts, checkoutStoreCart } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { useStoreCart } from "@/hooks/use-store-cart";
import { toast } from "sonner";

export default function StorePage() {
  const [showCart, setShowCart] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ["store-products"],
    queryFn: () => getStoreProducts(1, 50),
  });
  const products = data?.results ?? [];

  const { cart, addToCart, updateQuantity, removeFromCart, cartTotal, cartCount } = useStoreCart(products);

  const checkoutMut = useMutation({
    mutationFn: () => {
      const callbackUrl = `${window.location.origin}/store/callback`;
      return checkoutStoreCart(cart.map((l) => ({ productId: l.productId, quantity: l.quantity })), callbackUrl);
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
        <Button variant="outline" className="gap-2 shrink-0" onClick={() => setShowCart((v) => !v)}>
          <ShoppingCart size={16} />
          Cart{cartCount > 0 ? ` (${cartCount})` : ""}
        </Button>
      </div>

      {showCart && cart.length > 0 && (
        <Card className="border-primary/30">
          <CardContent className="p-4 space-y-3">
            {cart.map((l) => (
              <div key={l.productId} className="flex items-center gap-3">
                <Link href={`/store/${l.productId}`} className="w-12 h-12 rounded-lg bg-muted/50 shrink-0 overflow-hidden flex items-center justify-center">
                  {l.product.imageUrls?.[0] ? (
                    <img src={l.product.imageUrls[0]} alt={l.product.name} className="w-full h-full object-cover" />
                  ) : (
                    <Package size={18} className="text-muted-foreground" />
                  )}
                </Link>
                <div className="flex-1 min-w-0">
                  <Link href={`/store/${l.productId}`} className="text-[13px] font-semibold truncate block hover:underline">{l.product.name}</Link>
                  <p className="text-[12px] text-muted-foreground">{formatCurrency(l.product.price)} each</p>
                </div>
                <div className="flex items-center gap-1.5 shrink-0">
                  <button className="w-7 h-7 rounded-md border border-border flex items-center justify-center hover:bg-muted" onClick={() => updateQuantity(l.productId, -1)}>
                    <Minus size={12} />
                  </button>
                  <span className="w-6 text-center text-[13px] font-semibold">{l.quantity}</span>
                  <button className="w-7 h-7 rounded-md border border-border flex items-center justify-center hover:bg-muted" onClick={() => updateQuantity(l.productId, 1)}>
                    <Plus size={12} />
                  </button>
                  <button className="w-7 h-7 rounded-md flex items-center justify-center text-destructive hover:bg-destructive/10 ml-1" onClick={() => removeFromCart(l.productId)}>
                    <X size={13} />
                  </button>
                </div>
              </div>
            ))}
            <div className="flex items-center justify-between pt-3 border-t border-border">
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
          </CardContent>
        </Card>
      )}

      {isLoading ? (
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
          {Array.from({ length: 8 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : products.length === 0 ? (
        <EmptyState icon={<ShoppingBag size={40} />} title="No products yet" description="Check back soon — the alumni store is empty for now." />
      ) : (
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
          {products.map((p) => {
            const inCart = cart.find((l) => l.productId === p.id);
            const soldOut = p.quantityAvailable <= 0;
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
                  <p className="text-[14px] font-bold text-primary">{formatCurrency(p.price)}</p>
                  {soldOut ? (
                    <p className="text-[11px] text-destructive font-medium">Sold out</p>
                  ) : (
                    <p className="text-[11px] text-muted-foreground">{p.quantityAvailable} left</p>
                  )}
                  <Button
                    size="sm"
                    className="mt-auto w-full text-[12px] font-bold gap-1.5"
                    disabled={soldOut}
                    onClick={() => addToCart(p)}
                  >
                    <ShoppingCart size={13} />
                    {inCart ? `In cart (${inCart.quantity})` : "Add to cart"}
                  </Button>
                </CardContent>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}
