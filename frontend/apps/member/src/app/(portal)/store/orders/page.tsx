"use client";

import { useQuery } from "@tanstack/react-query";
import { ShoppingBag, CheckCircle2, Circle } from "@alumni/ui";
import { Card, CardContent } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
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

export default function MyStoreOrdersPage() {
  const { data, isLoading } = useQuery({
    queryKey: ["my-store-orders"],
    queryFn: () => getMyStoreOrders(1, 50),
  });
  const orders = data?.results ?? [];

  return (
    <div className="p-4 sm:p-6 lg:p-8 max-w-[900px] mx-auto space-y-6">
      <PageHeader eyebrow="Store" title="My orders" description="Orders you've placed in the alumni store." />

      {isLoading ? (
        <div className="space-y-3">
          {Array.from({ length: 4 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : orders.length === 0 ? (
        <EmptyState icon={<ShoppingBag size={40} />} title="No orders yet" description="Items you buy from the store will show up here." />
      ) : (
        <div className="space-y-3">
          {orders.map((o) => (
            <Card key={o.id}>
              <CardContent className="p-4 space-y-2">
                <div className="flex items-start justify-between gap-2 flex-wrap">
                  <div>
                    <p className="text-[13px] font-semibold">Order #{o.orderNumber}</p>
                    <p className="text-[12px] text-muted-foreground">{formatDate(o.createdAt)}</p>
                  </div>
                  <div className="flex items-center gap-2">
                    <Badge variant={statusVariant[o.status] ?? "secondary"} size="sm">{o.status}</Badge>
                    <span className="text-[14px] font-bold">{formatCurrency(o.totalAmount)}</span>
                  </div>
                </div>
                <div className="divide-y divide-border/40 border-t border-border/40 pt-2">
                  {o.items.map((item, i) => {
                    const label = variantLabel(item.variantOptions);
                    return (
                      <div key={i} className="py-2 space-y-1">
                        <div className="flex items-center justify-between gap-2 text-[13px]">
                          <span className="font-medium text-foreground">
                            {item.productName}
                            {label && <span className="text-muted-foreground"> ({label})</span>}
                            {" "}× {item.quantity}
                          </span>
                          <span className="text-muted-foreground">{formatCurrency(item.unitPrice * item.quantity)}</span>
                        </div>
                        {o.status === "Successful" && item.deliveryInfo && (
                          <p className="text-[11.5px] text-muted-foreground leading-relaxed">{item.deliveryInfo}</p>
                        )}
                      </div>
                    );
                  })}
                </div>

                {o.deliveryStatus && (
                  <div className="pt-3 border-t border-border/40 space-y-2.5">
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
                                  <CheckCircle2 size={14} className="text-primary shrink-0" />
                                ) : (
                                  <Circle size={14} className="text-primary/60 shrink-0" fill="currentColor" fillOpacity={0.15} />
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
      )}
    </div>
  );
}
