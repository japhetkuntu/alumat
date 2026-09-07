"use client";

import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { ShoppingBag, CheckCircle2, Circle, Package } from "@alumni/ui";
import { Card, CardContent, StatCard } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { Skeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { formatCurrency, formatDate } from "@alumni/ui";
import { getMyStoreOrders } from "@/lib/member-api";

const statusVariant: Record<string, "success" | "warning" | "destructive"> = {
  Successful: "success",
  Pending: "warning",
  Failed: "destructive",
};

const deliveryStatusVariant: Record<string, "success" | "warning" | "info" | "secondary"> = {
  Delivered: "success",
  Shipped: "info",
  Processing: "warning",
};

function variantLabel(options?: Record<string, string>) {
  if (!options) return null;
  const values = Object.values(options).filter(Boolean);
  return values.length > 0 ? values.join(" / ") : null;
}

function OrderSkeleton() {
  return (
    <Card>
      <CardContent className="p-5 space-y-4">
        <div className="flex items-start justify-between gap-3">
          <div className="space-y-2">
            <Skeleton className="h-4 w-32" variant="text" />
            <Skeleton className="h-3 w-24" variant="text" />
          </div>
          <Skeleton className="h-6 w-20" />
        </div>
        <div className="flex gap-3">
          <Skeleton className="h-14 w-14 shrink-0" />
          <div className="flex-1 space-y-2 pt-1">
            <Skeleton className="h-3.5 w-2/3" variant="text" />
            <Skeleton className="h-3 w-1/3" variant="text" />
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

export default function MyStoreOrdersPage() {
  const { data, isLoading } = useQuery({
    queryKey: ["my-store-orders"],
    queryFn: () => getMyStoreOrders(1, 50),
  });
  const orders = data?.results ?? [];

  const stats = useMemo(() => {
    const successful = orders.filter((o) => o.status === "Successful");
    const totalSpent = successful.reduce((sum, o) => sum + o.totalAmount, 0);
    const pending = orders.filter((o) => o.status === "Pending").length;
    return { totalOrders: orders.length, totalSpent, pending };
  }, [orders]);

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[1400px] mx-auto space-y-6 sm:space-y-8">
      <PageHeader eyebrow="Store" title="My orders" description="Everything you've bought from the alumni store, in one place." />

      {isLoading ? (
        <>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 sm:gap-4">
            {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-[92px]" />)}
          </div>
          <div className="space-y-3">
            {Array.from({ length: 4 }).map((_, i) => <OrderSkeleton key={i} />)}
          </div>
        </>
      ) : orders.length === 0 ? (
        <EmptyState
          icon={<ShoppingBag size={32} />}
          title="No orders yet"
          description="Items you buy from the alumni store will show up here, along with their delivery status."
        />
      ) : (
        <>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 sm:gap-4">
            <StatCard label="Total orders" value={stats.totalOrders} />
            <StatCard label="Total spent" value={formatCurrency(stats.totalSpent)} tone="accent" />
            <StatCard
              label="Pending"
              value={stats.pending}
              sub={stats.pending > 0 ? "Awaiting payment confirmation" : "All caught up"}
            />
          </div>

          <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">
            {orders.map((o) => (
              <Card key={o.id} className="overflow-hidden">
                <CardContent className="p-5 space-y-4">
                  <div className="flex items-start justify-between gap-3 flex-wrap">
                    <div>
                      <p className="text-[14px] font-semibold">Order #{o.orderNumber}</p>
                      <p className="text-[12px] text-muted-foreground mt-0.5">{formatDate(o.createdAt)}</p>
                    </div>
                    <div className="flex items-center gap-2">
                      <Badge variant={statusVariant[o.status] ?? "secondary"} size="sm">{o.status}</Badge>
                      <span className="text-[15px] font-bold tabular-nums">{formatCurrency(o.totalAmount)}</span>
                    </div>
                  </div>

                  <div className="space-y-3 border-t border-border/50 pt-3.5">
                    {o.items.map((item, i) => {
                      const label = variantLabel(item.variantOptions);
                      return (
                        <div key={i} className="flex items-center gap-3">
                          <div className="h-14 w-14 rounded-lg overflow-hidden shrink-0 flex items-center justify-center bg-muted/60 border border-border/40">
                            {item.productImageUrl ? (
                              // eslint-disable-next-line @next/next/no-img-element
                              <img src={item.productImageUrl} alt={item.productName} className="h-full w-full object-cover" loading="lazy" />
                            ) : (
                              <Package size={18} className="text-muted-foreground/50" />
                            )}
                          </div>
                          <div className="min-w-0 flex-1">
                            <p className="text-[13.5px] font-medium text-foreground truncate">
                              {item.productName}
                              {label && <span className="text-muted-foreground"> ({label})</span>}
                            </p>
                            <p className="text-[12px] text-muted-foreground mt-0.5">
                              Qty {item.quantity} &middot; {formatCurrency(item.unitPrice)} each
                            </p>
                            {o.status === "Successful" && item.deliveryInfo && (
                              <p className="text-[11.5px] text-muted-foreground leading-relaxed mt-1">{item.deliveryInfo}</p>
                            )}
                          </div>
                          <span className="text-[13px] font-semibold tabular-nums shrink-0">
                            {formatCurrency(item.unitPrice * item.quantity)}
                          </span>
                        </div>
                      );
                    })}
                  </div>

                  {o.deliveryStatus && (
                    <div className="pt-3.5 border-t border-border/50 space-y-2.5">
                      <div className="flex items-center gap-2">
                        <span className="text-[12px] font-semibold text-foreground">Delivery status</span>
                        <Badge variant={deliveryStatusVariant[o.deliveryStatus] ?? "secondary"} size="sm">{o.deliveryStatus}</Badge>
                      </div>
                      {o.deliveryStatusHistory.length > 0 && (
                        <ol className="space-y-2">
                          {o.deliveryStatusHistory.map((event, i) => {
                            const isLast = i === o.deliveryStatusHistory.length - 1;
                            return (
                              <li key={i} className="flex items-start gap-2.5">
                                <div className="flex flex-col items-center">
                                  {isLast ? (
                                    <CheckCircle2 size={14} className="text-accent shrink-0" />
                                  ) : (
                                    <Circle size={14} className="text-accent/60 shrink-0" fill="currentColor" fillOpacity={0.15} />
                                  )}
                                  {i < o.deliveryStatusHistory.length - 1 && (
                                    <div className="w-px flex-1 min-h-[10px] bg-border mt-0.5" />
                                  )}
                                </div>
                                <div className="pb-1">
                                  <p className="text-[12px] font-medium text-foreground leading-tight">{event.status}</p>
                                  <p className="text-[11px] text-muted-foreground">{formatDate(event.changedAt)}</p>
                                </div>
                              </li>
                            );
                          })}
                        </ol>
                      )}
                    </div>
                  )}
                </CardContent>
              </Card>
            ))}
          </div>
        </>
      )}
    </div>
  );
}
