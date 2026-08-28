"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
import type { CartItem } from "@/lib/member-api";
import type { StoreProduct } from "@/types";

export interface CartLine extends CartItem {
  product: StoreProduct;
}

// Scoped by institution slug so switching tenants (or the dev workspace override)
// never leaks one institution's cart into another's.
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

/** Shared cart state (localStorage-backed) used by both the store grid and a product's detail page, so adding an item from either place stays in sync. */
export function useStoreCart(liveProducts?: StoreProduct[]) {
  // Lazy-init from localStorage; SSR always starts empty and hydrates client-side
  // via the effect below, avoiding a hydration mismatch.
  const [cart, setCart] = useState<CartLine[]>([]);

  useEffect(() => {
    const restored = loadCart();
    if (restored.length > 0) setCart(restored);
  }, []);

  useEffect(() => {
    const key = cartStorageKey();
    if (!key) return;
    if (cart.length === 0) {
      localStorage.removeItem(key);
    } else {
      localStorage.setItem(key, JSON.stringify(cart));
    }
  }, [cart]);

  // Reconcile a restored cart against live product data once it loads — drop
  // lines for products that vanished, and clamp quantities to current stock.
  useEffect(() => {
    if (!liveProducts) return;
    setCart((prev) => prev
      .map((l) => {
        const live = liveProducts.find((p) => p.id === l.productId);
        if (!live || live.quantityAvailable <= 0) return null;
        const quantity = Math.min(l.quantity, live.quantityAvailable);
        return { ...l, product: live, quantity };
      })
      .filter((l): l is CartLine => l !== null));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [liveProducts]);

  const addToCart = (product: StoreProduct) => {
    setCart((prev) => {
      const existing = prev.find((l) => l.productId === product.id);
      if (existing) {
        if (existing.quantity >= product.quantityAvailable) {
          toast.error(`Only ${product.quantityAvailable} available`);
          return prev;
        }
        return prev.map((l) => (l.productId === product.id ? { ...l, quantity: l.quantity + 1 } : l));
      }
      return [...prev, { productId: product.id, quantity: 1, product }];
    });
  };

  const updateQuantity = (productId: string, delta: number) => {
    setCart((prev) => prev
      .map((l) => {
        if (l.productId !== productId) return l;
        const next = l.quantity + delta;
        if (next > l.product.quantityAvailable) {
          toast.error(`Only ${l.product.quantityAvailable} available`);
          return l;
        }
        return { ...l, quantity: next };
      })
      .filter((l) => l.quantity > 0));
  };

  const removeFromCart = (productId: string) => setCart((prev) => prev.filter((l) => l.productId !== productId));

  const cartTotal = cart.reduce((sum, l) => sum + l.product.price * l.quantity, 0);
  const cartCount = cart.reduce((sum, l) => sum + l.quantity, 0);

  return { cart, addToCart, updateQuantity, removeFromCart, cartTotal, cartCount };
}
