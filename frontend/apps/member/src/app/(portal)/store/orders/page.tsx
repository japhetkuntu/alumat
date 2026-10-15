"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { ShoppingBag, Package, ChevronDown, Clock, XCircle } from "@alumni/ui";
import { Badge, Button, EmptyState, FitImage, LoadError, PageHeader, Skeleton, cn, formatCurrency, formatDate } from "@alumni/ui";
import { getMyStoreOrders, getStoreOrderStatus } from "@/lib/member-api";
import { handleApiError } from "@/lib/api-client";
import { FilterTabs, StepList, StepProgress, UpdateTimeline } from "@/components/member/history-ui";
import type { StoreOrder, StoreOrderItem } from "@/types";

type Filter = "all" | "active" | "done" | "unpaid";

/** An order is in one place: waiting on payment, failed, being worked on, or finished. */
function stateOf(o: StoreOrder): "unpaid" | "failed" | "active" | "done" {
  if (o.status === "Pending") return "unpaid";
  if (o.status === "Failed") return "failed";
  const staged = o.items.filter((i) => (i.stages?.length ?? 0) > 0);
  if (staged.length > 0) {
    return staged.every((i) => i.currentStage === i.stages[i.stages.length - 1]) ? "done" : "active";
  }
  if (o.deliveryStatus) return o.deliveryStatus === "Delivered" ? "done" : "active";
  return "done";
}

const stateBadge = {
  unpaid: { label: "Awaiting payment", variant: "warning" },
  failed: { label: "Payment failed", variant: "destructive" },
  active: { label: "In progress", variant: "info" },
  done: { label: "Complete", variant: "success" },
} as const;

function variantLabel(options?: Record<string, string>) {
  const values = Object.values(options ?? {}).filter(Boolean);
  return values.length > 0 ? values.join(" / ") : null;
}

function ItemRow({ item }: { item: StoreOrderItem }) {
  const label = variantLabel(item.variantOptions);
  return (
    <li className="flex items-center gap-3">
      <Link href={`/store/${item.productId}`} className="flex size-12 shrink-0 items-center justify-center overflow-hidden border border-border bg-muted/50">
        {item.productImageUrl ? <FitImage src={item.productImageUrl} alt="" className="size-full" /> : <Package size={16} className="text-muted-foreground" />}
      </Link>
      <div className="min-w-0 flex-1">
        <Link href={`/store/${item.productId}`} className="block break-words text-[13.5px] font-semibold leading-snug hover:underline">{item.productName}</Link>
        <p className="text-[12.5px] text-muted-foreground">
          {label ? `${label} · ` : ""}{item.quantity} × {formatCurrency(item.unitPrice)}
        </p>
      </div>
      <span className="shrink-0 text-[13.5px] font-semibold tabular-nums">{formatCurrency(item.unitPrice * item.quantity)}</span>
    </li>
  );
}

function OrderCard({ order, onChecked }: { order: StoreOrder; onChecked: () => void }) {
  const state = stateOf(order);
  const [open, setOpen] = useState(state === "active");
  const [checking, setChecking] = useState(false);
  const staged = order.items.filter((i) => (i.stages?.length ?? 0) > 0);
  const hasDetails = order.items.some((i) => i.answers?.length > 0 || i.updates?.length > 0 || i.deliveryInfo)
    || staged.length > 0 || order.deliveryStatusHistory.length > 0;
  const badge = stateBadge[state];

  async function checkPayment() {
    if (!order.transactionRef) { toast.error("There is no payment reference to check for this order."); return; }
    setChecking(true);
    try {
      const r = await getStoreOrderStatus(order.transactionRef);
      toast(r.message || `Payment is ${r.status}`);
      onChecked();
    } catch (e) {
      toast.error(handleApiError(e));
    } finally {
      setChecking(false);
    }
  }

  return (
    <li className="border border-border bg-card">
      <div className="flex flex-wrap items-start justify-between gap-x-4 gap-y-2 p-4 pb-3 sm:p-5 sm:pb-3">
        <div className="min-w-0">
          <h2 className="text-[15px] font-bold">Order #{order.orderNumber}</h2>
          <p className="text-[12.5px] text-muted-foreground">{formatDate(order.createdAt)}</p>
        </div>
        <div className="flex items-center gap-3">
          <Badge variant={badge.variant} size="sm">{badge.label}</Badge>
          <span className="text-[16px] font-bold tabular-nums">{formatCurrency(order.totalAmount)}</span>
        </div>
      </div>

      <ul className="space-y-3 px-4 sm:px-5">
        {order.items.map((item, i) => <ItemRow key={`${item.productId}:${item.variantId ?? ""}:${i}`} item={item} />)}
      </ul>

      <div className="space-y-3 p-4 sm:p-5">
        {state === "unpaid" && (
          <div className="flex flex-wrap items-center justify-between gap-3 border border-warning/40 bg-warning/5 p-3">
            <p className="flex items-start gap-2 text-[13px] text-muted-foreground">
              <Clock size={15} className="mt-0.5 shrink-0 text-warning" />
              We have not had confirmation of this payment yet. It starts moving once the payment clears.
            </p>
            <Button size="sm" variant="outline" isLoading={checking} loadingText="Checking" onClick={checkPayment}>Check payment</Button>
          </div>
        )}
        {state === "failed" && (
          <p className="flex items-start gap-2 border border-destructive/30 bg-destructive/5 p-3 text-[13px] text-muted-foreground">
            <XCircle size={15} className="mt-0.5 shrink-0 text-destructive" />
            The payment did not go through, so this order was not placed. Nothing was charged.
          </p>
        )}

        {state !== "unpaid" && state !== "failed" && staged.map((item, i) => {
          const current = item.stages.indexOf(item.currentStage ?? "");
          const done = item.currentStage === item.stages[item.stages.length - 1];
          return (
            <div key={i} className="space-y-1.5">
              {staged.length > 1 && <p className="text-[12.5px] font-semibold text-muted-foreground">{item.productName}</p>}
              <StepProgress stages={item.stages} current={current} done={done} />
            </div>
          );
        })}

        {state !== "unpaid" && state !== "failed" && staged.length === 0 && order.deliveryStatus && (
          <p className="text-[13px]">
            <span className="font-semibold">{order.deliveryStatus}</span>
            {order.deliveryStatusUpdatedAt && <span className="text-muted-foreground"> · {formatDate(order.deliveryStatusUpdatedAt)}</span>}
          </p>
        )}

        {hasDetails && state !== "unpaid" && state !== "failed" && (
          <>
            <button
              type="button"
              aria-expanded={open}
              onClick={() => setOpen((v) => !v)}
              className="inline-flex min-h-10 items-center gap-1.5 text-[13px] font-semibold text-primary focus-visible:outline focus-visible:outline-2 focus-visible:outline-primary"
            >
              <ChevronDown size={14} className={cn("transition-transform", open && "rotate-180")} />
              {open ? "Hide details" : "Details and updates"}
            </button>

            {open && (
              <div className="space-y-5 border-t border-border pt-4">
                {order.items.map((item, i) => {
                  const hasAny = item.answers?.length > 0 || item.updates?.length > 0 || (item.stages?.length ?? 0) > 0 || item.deliveryInfo;
                  if (!hasAny) return null;
                  const current = item.stages.indexOf(item.currentStage ?? "");
                  const done = item.stages.length > 0 && item.currentStage === item.stages[item.stages.length - 1];
                  return (
                    <section key={i} className="space-y-3">
                      {order.items.length > 1 && <h3 className="text-[13.5px] font-bold">{item.productName}</h3>}
                      {item.deliveryInfo && <p className="text-[13px] leading-relaxed text-muted-foreground">{item.deliveryInfo}</p>}
                      {item.answers?.length > 0 && (
                        <dl className="grid grid-cols-1 gap-x-6 gap-y-2 sm:grid-cols-2">
                          {item.answers.map((a) => (
                            <div key={`${a.section}:${a.key}`} className="min-w-0">
                              <dt className="text-[12px] text-muted-foreground">{a.label}</dt>
                              <dd className="break-words text-[13.5px] font-medium">
                                {a.type === "File" ? <a href={a.value} target="_blank" rel="noreferrer" className="text-primary underline underline-offset-2">View file</a> : a.value || "—"}
                              </dd>
                            </div>
                          ))}
                        </dl>
                      )}
                      {item.stages?.length > 0 && <StepList stages={item.stages} current={current} done={done} />}
                      <UpdateTimeline updates={item.updates ?? []} />
                    </section>
                  );
                })}

                {staged.length === 0 && order.deliveryStatusHistory.length > 0 && (
                  <section className="space-y-2">
                    <h3 className="text-[13.5px] font-bold">Delivery</h3>
                    <ol className="space-y-1.5">
                      {[...order.deliveryStatusHistory].reverse().map((e, i) => (
                        <li key={i} className="flex flex-wrap items-baseline justify-between gap-2 text-[13px]">
                          <span className={cn(i === 0 && "font-semibold")}>{e.status}</span>
                          <span className="text-[12px] text-muted-foreground">{formatDate(e.changedAt)}</span>
                        </li>
                      ))}
                    </ol>
                  </section>
                )}
              </div>
            )}
          </>
        )}
      </div>
    </li>
  );
}

export default function MyStoreOrdersPage() {
  const { data, isLoading, isError, refetch } = useQuery({ queryKey: ["my-store-orders"], queryFn: () => getMyStoreOrders(1, 50) });
  const orders = useMemo(() => data?.results ?? [], [data]);
  const [filter, setFilter] = useState<Filter>("all");

  const counts = useMemo(() => {
    const c = { all: orders.length, active: 0, done: 0, unpaid: 0 };
    for (const o of orders) { const s = stateOf(o); if (s === "active") c.active++; else if (s === "done") c.done++; else if (s === "unpaid") c.unpaid++; }
    return c;
  }, [orders]);
  const spent = useMemo(() => orders.filter((o) => o.status === "Successful").reduce((n, o) => n + o.totalAmount, 0), [orders]);
  const shown = orders.filter((o) => filter === "all" || (filter === "unpaid" ? stateOf(o) === "unpaid" : stateOf(o) === filter));

  return (
    <div className="mx-auto max-w-[860px] space-y-5 p-4 sm:p-6 lg:p-8">
      <PageHeader eyebrow="Store" title="My orders" description="Everything you have bought from the store, and where each order is." />

      {isLoading ? (
        <div className="space-y-3">{Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-40 w-full" />)}</div>
      ) : isError || !data ? (
        <LoadError onRetry={() => refetch()} />
      ) : orders.length === 0 ? (
        <EmptyState
          icon={<ShoppingBag size={32} />}
          title="No orders yet"
          description="Things you buy from the store show up here, from payment through to delivery."
          action={<Link href="/store"><Button size="sm" className="font-semibold">Browse the store</Button></Link>}
        />
      ) : (
        <>
          <p className="text-[13.5px] text-muted-foreground">
            {orders.length} {orders.length === 1 ? "order" : "orders"} · {formatCurrency(spent)} paid
            {counts.unpaid > 0 && <span className="font-semibold text-warning"> · {counts.unpaid} awaiting payment</span>}
          </p>

          <FilterTabs
            label="Filter orders"
            value={filter}
            onChange={setFilter}
            options={[
              { value: "all", label: "All", count: counts.all },
              { value: "active", label: "In progress", count: counts.active },
              { value: "done", label: "Complete", count: counts.done },
              { value: "unpaid", label: "Awaiting payment", count: counts.unpaid },
            ]}
          />

          {shown.length === 0 ? (
            <p className="border border-dashed border-border p-6 text-center text-[13.5px] text-muted-foreground">Nothing here right now.</p>
          ) : (
            <ul className="space-y-3">
              {shown.map((o) => <OrderCard key={o.id} order={o} onChecked={() => void refetch()} />)}
            </ul>
          )}
        </>
      )}
    </div>
  );
}
