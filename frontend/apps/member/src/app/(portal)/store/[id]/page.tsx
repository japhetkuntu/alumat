"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, ShoppingCart, Package, Truck, Plus, Minus } from "lucide-react";
import { Button } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { Skeleton } from "@alumni/ui";
import { formatCurrency } from "@alumni/ui";
import { getStoreProduct } from "@/lib/member-api";
import { useStoreCart } from "@/hooks/use-store-cart";

export default function StoreProductDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const [activeImage, setActiveImage] = useState(0);

  const { data: product, isLoading, isError } = useQuery({
    queryKey: ["store-product", id],
    queryFn: () => getStoreProduct(id),
  });

  const { cart, addToCart, updateQuantity } = useStoreCart();
  const inCart = cart.find((l) => l.productId === id);
  const soldOut = !!product && product.quantityAvailable <= 0;
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
            {images[activeImage] ? (
              <img src={images[activeImage]} alt={product.name} className="w-full h-full object-cover" />
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
            <p className="text-[20px] font-bold text-primary mt-1">{formatCurrency(product.price)}</p>
            {soldOut ? (
              <p className="text-[12.5px] text-destructive font-medium mt-1">Sold out</p>
            ) : (
              <p className="text-[12.5px] text-muted-foreground mt-1">{product.quantityAvailable} available</p>
            )}
          </div>

          {product.description && (
            <p className="text-[13.5px] text-muted-foreground leading-relaxed">{product.description}</p>
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
                <button className="w-9 h-9 rounded-md border border-border flex items-center justify-center hover:bg-muted" onClick={() => updateQuantity(product.id, -1)}>
                  <Minus size={14} />
                </button>
                <span className="w-8 text-center text-[15px] font-semibold">{inCart.quantity}</span>
                <button className="w-9 h-9 rounded-md border border-border flex items-center justify-center hover:bg-muted" onClick={() => updateQuantity(product.id, 1)}>
                  <Plus size={14} />
                </button>
              </div>
            ) : (
              <Button className="gap-2 font-semibold" disabled={soldOut} onClick={() => addToCart(product)}>
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
