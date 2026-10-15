"use client";

import { NotifyMeButton } from "@/components/member/notify-me-button";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { ShoppingBag, ShoppingCart, Package, Receipt } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { formatCurrency } from "@alumni/ui";
import { getStoreProducts } from "@/lib/member-api";
import { useStoreCart } from "@/hooks/use-store-cart";
import { productNeedsForm } from "@/components/member/store-item-form";
import { StoreCartBar } from "@/components/member/store-cart-bar";
import type { StoreProduct } from "@/types";
import { toast } from "sonner";
import { useNavTheme } from "@/components/member/member-layout";
import { FitImage } from "@alumni/ui";

export default function StorePage() {
  const { data, isLoading, isSuccess } = useQuery({
    queryKey: ["store-products"],
    queryFn: () => getStoreProducts(1, 50),
  });
  const products = data?.results ?? [];
  // Pass undefined (not []) until the list has actually loaded — the cart hook treats an empty array
  // as "confirmed no products exist" and reconciles cart lines against it, which would wipe every
  // line while loading, and also if the request fails.
  const liveProducts = isSuccess ? products : undefined;

  const { cart, addToCart, cartCount } = useStoreCart(liveProducts);
  const { data: navTheme } = useNavTheme();
  const isCommunity = navTheme?.organizationType === "Community";

  // Adding gives a quiet confirmation with a way straight to the cart, so nothing has to be hunted for.
  const router = useRouter();
  const add = (p: StoreProduct) => {
    addToCart(p);
    if (productNeedsForm(p)) { router.push("/store/cart"); return; }
    toast.success(`${p.name} added`, { action: { label: "View cart", onClick: () => router.push("/store/cart") } });
  };

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
          <Link href="/store/cart">
            <Button variant={cartCount > 0 ? "default" : "outline"} className="gap-2">
              <ShoppingCart size={16} />
              Cart{cartCount > 0 ? ` (${cartCount})` : ""}
            </Button>
          </Link>
        </div>
      </div>

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
                    <p className="text-[12px] text-destructive font-medium">Sold out</p>
                  ) : !hasVariants && tracked ? (
                    <p className="text-[12px] text-muted-foreground">{p.quantityAvailable} left</p>
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
                      onClick={() => add(p)}
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
      <StoreCartBar />
    </div>
  );
}
