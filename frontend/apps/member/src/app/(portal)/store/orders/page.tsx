"use client";

import { useQuery } from "@tanstack/react-query";
import { ShoppingBag } from "lucide-react";
import { Card, CardContent } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { PageHeader } from "@alumni/ui";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import { formatCurrency, formatDate } from "@alumni/ui";
import { getMyStoreOrders } from "@/lib/member-api";

const statusVariant: Record<string, "success" | "warning" | "destructive"> = {
  Confirmed: "success",
  Pending: "warning",
  Failed: "destructive",
};

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
                  <p className="text-[12px] text-muted-foreground">{formatDate(o.createdAt)}</p>
                  <div className="flex items-center gap-2">
                    <Badge variant={statusVariant[o.status] ?? "secondary"} size="sm">{o.status}</Badge>
                    <span className="text-[14px] font-bold">{formatCurrency(o.totalAmount)}</span>
                  </div>
                </div>
                <div className="divide-y divide-border/40 border-t border-border/40 pt-2">
                  {o.items.map((item, i) => (
                    <div key={i} className="py-2 space-y-1">
                      <div className="flex items-center justify-between gap-2 text-[13px]">
                        <span className="font-medium text-foreground">{item.productName} × {item.quantity}</span>
                        <span className="text-muted-foreground">{formatCurrency(item.unitPrice * item.quantity)}</span>
                      </div>
                      {o.status === "Confirmed" && item.deliveryInfo && (
                        <p className="text-[11.5px] text-muted-foreground leading-relaxed">{item.deliveryInfo}</p>
                      )}
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
