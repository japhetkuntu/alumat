"use client";

import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import type { CartItem } from "@/lib/member-api";
import type { StoreProduct, StoreProductVariant } from "@/types";

export interface CartLine extends CartItem {
  product: StoreProduct;
  /** Snapshot of the selected variant, when the line is for a variant product. */
  variant?: StoreProductVariant;
}

function cartStorageKey() {
  if (typeof window === "undefined") return null;
  const slug = localStorage.getItem("institution_slug");
  return `alumni-store-cart:${slug ?? "default"}`;
}

function loadCart(): CartLine[] {
  const key = cartStorageKey();
  if (!key) return [];
  try {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as CartLine[]) : [];
  } catch {
    return [];
  }
}

function sameLine(a: { productId: string; variantId?: string }, b: { productId: string; variantId?: string }) {
  return a.productId === b.productId && (a.variantId ?? null) === (b.variantId ?? null);
}

/** Effective unit price/stock for a line — the variant's if present, else the product's. */
function lineStock(product: StoreProduct, variant?: StoreProductVariant) {
  return variant ? variant.quantityAvailable : product.quantityAvailable;
}

export function lineUnitPrice(product: StoreProduct, variant?: StoreProductVariant) {
  return variant ? variant.price : product.price;
}

export function useStoreCart(liveProducts?: StoreProduct[]) {
  const [cart, setCart] = useState<CartLine[]>([]);
  // Guards against a hydration race: on mount `cart` is still `[]` (the pre-restore
  // value) in the same commit the restore effect below fires in — without this, the
  // write-back effect runs first with that stale empty array and wipes localStorage
  // out from under the restore that's about to land a render later.
  const skipNextWrite = useRef(true);

  useEffect(() => {
    const restored = loadCart();
    if (restored.length > 0) setCart(restored);
  }, []);

  useEffect(() => {
    if (skipNextWrite.current) {
      skipNextWrite.current = false;
      return;
    }
    const key = cartStorageKey();
    if (!key) return;
    if (cart.length === 0) {
      localStorage.removeItem(key);
    } else {
      localStorage.setItem(key, JSON.stringify(cart));
    }
  }, [cart]);

  useEffect(() => {
    if (!liveProducts) return;
    setCart((prev) => prev
      .map((l) => {
        const live = liveProducts.find((p) => p.id === l.productId);
        if (!live) return null;
        const liveVariant = l.variantId ? live.variants.find((v) => v.id === l.variantId) : undefined;
        if (l.variantId && !liveVariant) return null;
        const stock = lineStock(live, liveVariant);
        if (stock <= 0) return null;
        const quantity = Math.min(l.quantity, stock);
        const updated: CartLine = { ...l, product: live, variant: liveVariant, quantity };
        return updated;
      })
      .filter((l): l is CartLine => l !== null));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [liveProducts]);

  const addToCart = (product: StoreProduct, variant?: StoreProductVariant) => {
    setCart((prev) => {
      const key = { productId: product.id, variantId: variant?.id };
      const existing = prev.find((l) => sameLine(l, key));
      const stock = lineStock(product, variant);
      if (existing) {
        if (existing.quantity >= stock) {
          toast.error(`Only ${stock} available`);
          return prev;
        }
        return prev.map((l) => (sameLine(l, key) ? { ...l, quantity: l.quantity + 1 } : l));
      }
      if (stock <= 0) {
        toast.error("Out of stock");
        return prev;
      }
      return [...prev, { productId: product.id, variantId: variant?.id, quantity: 1, product, variant }];
    });
  };

  const updateQuantity = (productId: string, variantId: string | undefined, delta: number) => {
    setCart((prev) => prev
      .map((l) => {
        if (!sameLine(l, { productId, variantId })) return l;
        const stock = lineStock(l.product, l.variant);
        const next = l.quantity + delta;
        if (next > stock) {
          toast.error(`Only ${stock} available`);
          return l;
        }
        return { ...l, quantity: next };
      })
      .filter((l) => l.quantity > 0));
  };

  const removeFromCart = (productId: string, variantId?: string) =>
    setCart((prev) => prev.filter((l) => !sameLine(l, { productId, variantId })));

  const cartTotal = cart.reduce((sum, l) => sum + lineUnitPrice(l.product, l.variant) * l.quantity, 0);
  const cartCount = cart.reduce((sum, l) => sum + l.quantity, 0);

  return { cart, addToCart, updateQuantity, removeFromCart, cartTotal, cartCount };
}
