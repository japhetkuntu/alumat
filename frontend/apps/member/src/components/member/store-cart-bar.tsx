"use client";

import Link from "next/link";
import { ShoppingCart } from "@alumni/ui";
import { formatCurrency } from "@alumni/ui";
import { useCartSummary } from "@/hooks/use-store-cart";

/**
 * The way from browsing to the cart on a phone: a bar above the bottom navigation that only appears once something is in
 * the cart. On wider screens the header's cart button does this job, so the bar is hidden there.
 */
export function StoreCartBar() {
  const { count: cartCount, total: cartTotal } = useCartSummary();
  if (cartCount === 0) return null;
  return (
    <div className="fixed left-0 right-[4.75rem] z-30 pl-3 lg:hidden" style={{ bottom: "calc(4rem + env(safe-area-inset-bottom))" }}>
      <Link
        href="/store/cart"
        className="flex h-12 items-center justify-between gap-3 bg-primary px-4 text-[14px] font-semibold text-primary-foreground shadow-md focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
      >
        <span className="inline-flex items-center gap-2">
          <ShoppingCart size={16} />
          {cartCount} {cartCount === 1 ? "item" : "items"} · {formatCurrency(cartTotal)}
        </span>
        <span className="shrink-0">View cart</span>
      </Link>
    </div>
  );
}
