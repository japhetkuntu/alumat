"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, ShoppingCart, Package, Truck, Plus, Minus } from "lucide-react";
import { Button } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { Skeleton } from "@alumni/ui";
import { formatCurrency } from "@alumni/ui";
import { cn } from "@alumni/ui";
import { getStoreProduct } from "@/lib/member-api";
import { useStoreCart } from "@/hooks/use-store-cart";
import type { StoreProduct, StoreProductVariant } from "@/types";

/** Given the currently selected options, does at least one in-stock variant match `candidate` for `optionType`? */
function optionValueIsAvailable(
  product: StoreProduct,
  optionType: string,
  candidate: string,
  selected: Record<string, string>
) {
  return product.variants.some((v) => {
    if (v.options[optionType] !== candidate) return false;
    if (v.quantityAvailable <= 0) return false;
    return Object.entries(selected).every(([type, value]) => type === optionType || v.options[type] === value);
  });
}

function resolveVariant(product: StoreProduct, selected: Record<string, string>): StoreProductVariant | undefined {
  if (product.variantOptionTypes.some((t) => !selected[t])) return undefined;
  return product.variants.find((v) => product.variantOptionTypes.every((t) => v.options[t] === selected[t]));
}

export default function StoreProductDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const [activeImage, setActiveImage] = useState(0);
  const [selectedOptions, setSelectedOptions] = useState<Record<string, string>>({});

  const { data: product, isLoading, isError } = useQuery({
    queryKey: ["store-product", id],
    queryFn: () => getStoreProduct(id),
  });

  const hasVariants = !!product && product.variantOptionTypes.length > 0;
  const selectedVariant = useMemo(
    () => (product && hasVariants ? resolveVariant(product, selectedOptions) : undefined),
    [product, hasVariants, selectedOptions]
  );

  const { cart, addToCart, updateQuantity } = useStoreCart();
  const inCart = cart.find((l) => l.productId === id && (l.variantId ?? undefined) === (selectedVariant?.id ?? undefined));

  const effectivePrice = product ? (hasVariants ? selectedVariant?.price : product.price) : undefined;
  const effectiveStock = product ? (hasVariants ? selectedVariant?.quantityAvailable : product.quantityAvailable) : undefined;
  const soldOut = hasVariants ? (selectedVariant ? selectedVariant.quantityAvailable <= 0 : false) : !!product && product.quantityAvailable <= 0;
  const canAdd = hasVariants ? !!selectedVariant && selectedVariant.quantityAvailable > 0 : !soldOut;

  const displayImage = (hasVariants && selectedVariant?.imageUrl) || undefined;
  const images = product?.imageUrls?.length ? product.imageUrls : [];

  if (isLoading) {
    return (
      <div className="p-4 sm:p-6 lg:p-8 max-w-[1000px] mx-auto space-y-6">
        <Skeleton className="h-8 w-40" />
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-8">
          <Skeleton className="h-80 w-full rounded-xl" />
          <div className="space-y-3">
            <Skeleton className="h-6 w-2/3" />
            <Skeleton className="h-4 w-1/3" />
            <Skeleton className="h-24 w-full" />
          </div>
        </div>
      </div>
    );
  }

  if (isError || !product) {
    return (
      <div className="p-4 sm:p-6 lg:p-8 max-w-[1000px] mx-auto space-y-4">
        <Link href="/store" className="inline-flex items-center gap-1.5 text-[13px] text-muted-foreground hover:text-foreground">
          <ArrowLeft size={14} /> Back to store
        </Link>
        <p className="text-[14px] text-muted-foreground">This product couldn&apos;t be found — it may have been removed.</p>
      </div>
    );
  }

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1000px] mx-auto space-y-6">
      <button onClick={() => router.push("/store")} className="inline-flex items-center gap-1.5 text-[13px] text-muted-foreground hover:text-foreground">
        <ArrowLeft size={14} /> Back to store
      </button>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-8">
        <div className="space-y-2">
          <div className="w-full aspect-square rounded-xl overflow-hidden bg-muted/40 flex items-center justify-center">
            {displayImage || images[activeImage] ? (
              <img src={displayImage ?? images[activeImage]} alt={product.name} className="w-full h-full object-cover" />
            ) : (
              <Package size={48} className="text-muted-foreground" />
            )}
          </div>
          {images.length > 1 && (
            <div className="flex gap-2">
              {images.map((url, i) => (
                <button
                  key={url + i}
                  onClick={() => setActiveImage(i)}
                  className="w-14 h-14 rounded-lg overflow-hidden border-2 shrink-0"
                  style={{ borderColor: i === activeImage ? "var(--primary)" : "transparent" }}
                >
                  <img src={url} alt="" className="w-full h-full object-cover" />
                </button>
              ))}
            </div>
          )}
        </div>

        <div className="space-y-4">
          <div>
            <h1 className="text-[22px] font-bold leading-snug">{product.name}</h1>
            <p className="text-[20px] font-bold text-primary mt-1">
              {effectivePrice !== undefined ? formatCurrency(effectivePrice) : hasVariants ? "Select options" : formatCurrency(product.price)}
            </p>
            {hasVariants ? (
              selectedVariant ? (
                soldOut ? (
                  <p className="text-[12.5px] text-destructive font-medium mt-1">Sold out</p>
                ) : (
                  <p className="text-[12.5px] text-muted-foreground mt-1">{effectiveStock} available</p>
                )
              ) : (
                <p className="text-[12.5px] text-muted-foreground mt-1">Choose options to see price and availability</p>
              )
            ) : soldOut ? (
              <p className="text-[12.5px] text-destructive font-medium mt-1">Sold out</p>
            ) : (
              <p className="text-[12.5px] text-muted-foreground mt-1">{product.quantityAvailable} available</p>
            )}
          </div>

          {product.description && (
            <p className="text-[13.5px] text-muted-foreground leading-relaxed">{product.description}</p>
          )}

          {hasVariants && (
            <div className="space-y-3">
              {product.variantOptionTypes.map((optionType) => {
                const values = Array.from(new Set(product.variants.map((v) => v.options[optionType]).filter(Boolean)));
                return (
                  <div key={optionType} className="space-y-1.5">
                    <p className="text-[12px] font-semibold text-foreground">{optionType}</p>
                    <div className="flex flex-wrap gap-2">
                      {values.map((value) => {
                        const isSelected = selectedOptions[optionType] === value;
                        const isAvailable = optionValueIsAvailable(product, optionType, value, selectedOptions);
                        return (
                          <button
                            key={value}
                            type="button"
                            disabled={!isAvailable}
                            onClick={() => setSelectedOptions((prev) => ({ ...prev, [optionType]: value }))}
                            className={cn(
                              "h-9 min-w-[2.5rem] px-3 border text-[13px] font-medium transition-colors",
                              isSelected
                                ? "border-primary bg-primary text-primary-foreground"
                                : "border-border bg-background text-foreground hover:bg-muted",
                              !isAvailable && "opacity-40 cursor-not-allowed line-through hover:bg-background"
                            )}
                          >
                            {value}
                          </button>
                        );
                      })}
                    </div>
                  </div>
                );
              })}
            </div>
          )}

          {product.deliveryInfo && (
            <Card className="border-border/60">
              <CardContent className="p-3.5 flex items-start gap-2.5">
                <Truck size={16} className="text-primary shrink-0 mt-0.5" />
                <div>
                  <p className="text-[12.5px] font-semibold">Delivery &amp; pickup</p>
                  <p className="text-[12px] text-muted-foreground leading-relaxed mt-0.5">{product.deliveryInfo}</p>
                </div>
              </CardContent>
            </Card>
          )}

          <div className="flex items-center gap-3 pt-2">
            {inCart ? (
              <div className="flex items-center gap-2">
                <button
                  className="w-9 h-9 rounded-md border border-border flex items-center justify-center hover:bg-muted"
                  onClick={() => updateQuantity(product.id, selectedVariant?.id, -1)}
                >
                  <Minus size={14} />
                </button>
                <span className="w-8 text-center text-[15px] font-semibold">{inCart.quantity}</span>
                <button
                  className="w-9 h-9 rounded-md border border-border flex items-center justify-center hover:bg-muted"
                  onClick={() => updateQuantity(product.id, selectedVariant?.id, 1)}
                >
                  <Plus size={14} />
                </button>
              </div>
            ) : (
              <Button className="gap-2 font-semibold" disabled={!canAdd} onClick={() => addToCart(product, selectedVariant)}>
                <ShoppingCart size={15} />
                Add to cart
              </Button>
            )}
            <Link href="/store">
              <Button variant="outline">Continue shopping</Button>
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
