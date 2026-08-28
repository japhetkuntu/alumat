"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Package, Pencil, Trash2, ShoppingBag } from "lucide-react";
import { Pagination } from "@alumni/ui";
import { Button } from "@alumni/ui";
import { Input } from "@alumni/ui";
import { Label } from "@alumni/ui";
import { Textarea } from "@alumni/ui";
import { FormSelect } from "@alumni/ui";
import { Card, CardContent, CardHeader, CardTitle } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { ConfirmModal } from "@alumni/ui";
import { MultiImageUpload } from "@alumni/ui";
import { formatCurrency, formatDate } from "@alumni/ui";
import { cn } from "@alumni/ui";
import {
  getStoreProducts, createStoreProduct, updateStoreProduct, deleteStoreProduct, getStoreOrders,
  getStoreSettings, updateStoreSettings,
} from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import type { StoreProduct } from "@/types";

const statusVariant: Record<string, "success" | "secondary" | "warning"> = {
  Active: "success",
  Draft: "warning",
  Archived: "secondary",
};

const orderStatusVariant: Record<string, "success" | "secondary" | "warning" | "destructive"> = {
  Confirmed: "success",
  Pending: "warning",
  Failed: "destructive",
};

interface FormState {
  name: string;
  description: string;
  price: string;
  quantityAvailable: string;
  deliveryInfo: string;
  status: string;
  images: File[];
  existingImageUrls: string[];
}

const emptyForm: FormState = {
  name: "", description: "", price: "", quantityAvailable: "", deliveryInfo: "", status: "Active",
  images: [], existingImageUrls: [],
};

function ProductForm({ init, onSave, onCancel, saving, title, defaultDeliveryInfo }: {
  init: FormState; onSave: (f: FormState) => void; onCancel: () => void; saving: boolean; title: string; defaultDeliveryInfo?: string;
}) {
  const [form, setForm] = useState(init);
  const f = <K extends keyof FormState>(k: K, v: FormState[K]) => setForm((prev) => ({ ...prev, [k]: v }));

  return (
    <Card>
      <CardHeader><CardTitle className="text-base">{title}</CardTitle></CardHeader>
      <CardContent>
        <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); onSave(form); }}>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-2"><Label>Product name</Label>
              <Input placeholder="e.g. Alumni Hoodie" value={form.name} onChange={(e) => f("name", e.target.value)} required /></div>
            <div className="space-y-2"><Label>Status</Label>
              <FormSelect value={form.status} onValueChange={(v) => f("status", v)}
                options={[
                  { value: "Active", label: "Active — visible to members" },
                  { value: "Draft", label: "Draft — hidden" },
                  { value: "Archived", label: "Archived" },
                ]} /></div>
            <div className="space-y-2"><Label>Price (GHS)</Label>
              <Input type="number" min="0" step="0.01" placeholder="150" value={form.price} onChange={(e) => f("price", e.target.value)} required /></div>
            <div className="space-y-2"><Label>Quantity available</Label>
              <Input type="number" min="0" placeholder="50" value={form.quantityAvailable} onChange={(e) => f("quantityAvailable", e.target.value)} required /></div>
          </div>
          <div className="space-y-2"><Label>Description</Label>
            <Textarea placeholder="Describe the product..." rows={3} value={form.description} onChange={(e) => f("description", e.target.value)} /></div>
          <div className="space-y-2">
            <Label>Delivery info</Label>
            <p className="text-[12px] text-muted-foreground -mt-0.5">
              Shown to buyers — delivery itself is handled by your team outside the platform (e.g. pickup location/hours, shipping timeframe).
              {defaultDeliveryInfo && " Leave blank to use your store's default delivery info (set below)."}
            </p>
            <Textarea
              rows={2}
              value={form.deliveryInfo}
              onChange={(e) => f("deliveryInfo", e.target.value)}
              placeholder={defaultDeliveryInfo || "Pickup at the alumni office, Mon–Fri 9am–5pm."}
            />
          </div>
          <div className="space-y-2"><Label>Photos</Label>
            <MultiImageUpload files={form.images} existingUrls={form.existingImageUrls}
              onAddFile={(file) => setForm((prev) => ({ ...prev, images: [...prev.images, file] }))}
              onRemoveFile={(i) => setForm((prev) => ({ ...prev, images: prev.images.filter((_, idx) => idx !== i) }))}
              onRemoveExisting={(i) => setForm((prev) => ({ ...prev, existingImageUrls: prev.existingImageUrls.filter((_, idx) => idx !== i) }))}
              label="Add photo" /></div>
          <div className="flex gap-3">
            <Button type="submit" size="sm" isLoading={saving} loadingText="Saving">Save</Button>
            <Button type="button" size="sm" variant="outline" onClick={onCancel}>Cancel</Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}

export default function AdminStorePage() {
  const [tab, setTab] = useState<"Products" | "Orders">("Products");
  const [showCreate, setShowCreate] = useState(false);
  const [editProduct, setEditProduct] = useState<StoreProduct | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<StoreProduct | null>(null);
  const [statusFilter, setStatusFilter] = useState("");
  const [page, setPage] = useState(1);
  const [orderPage, setOrderPage] = useState(1);
  const pageSize = 20;
  const qc = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ["admin-store-products", statusFilter, page],
    queryFn: () => getStoreProducts(page, pageSize, statusFilter || undefined),
    placeholderData: (prev) => prev,
    enabled: tab === "Products",
  });

  const { data: orderData, isLoading: ordersLoading } = useQuery({
    queryKey: ["admin-store-orders", orderPage],
    queryFn: () => getStoreOrders(orderPage, pageSize),
    placeholderData: (prev) => prev,
    enabled: tab === "Orders",
  });

  const { data: settings } = useQuery({ queryKey: ["admin-store-settings"], queryFn: getStoreSettings });
  const [editingSettings, setEditingSettings] = useState(false);
  const [defaultDeliveryDraft, setDefaultDeliveryDraft] = useState("");
  const settingsMut = useMutation({
    mutationFn: (value: string) => updateStoreSettings(value),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-store-settings"] }); setEditingSettings(false); toast.success("Default delivery info updated"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const createMut = useMutation({
    mutationFn: (f: FormState) => createStoreProduct({
      name: f.name, description: f.description || undefined, price: Number(f.price),
      quantityAvailable: Number(f.quantityAvailable), deliveryInfo: f.deliveryInfo || undefined,
      status: f.status, images: f.images.length > 0 ? f.images : undefined,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-store-products"] }); setShowCreate(false); toast.success("Product created"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const updateMut = useMutation({
    mutationFn: ({ id, f }: { id: string; f: FormState }) => updateStoreProduct(id, {
      name: f.name, description: f.description || undefined, price: Number(f.price),
      quantityAvailable: Number(f.quantityAvailable), deliveryInfo: f.deliveryInfo || undefined,
      status: f.status, images: f.images.length > 0 ? f.images : undefined,
      existingImageUrls: f.existingImageUrls.length > 0 ? f.existingImageUrls : undefined,
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-store-products"] }); setEditProduct(null); toast.success("Product updated"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const deleteMut = useMutation({
    mutationFn: (id: string) => deleteStoreProduct(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-store-products"] }); setDeleteTarget(null); toast.success("Product deleted"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const products = data?.results ?? [];
  const totalPages = data?.totalPages ?? 1;
  const orders = orderData?.results ?? [];
  const orderTotalPages = orderData?.totalPages ?? 1;

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-5">
      <header className="flex items-end justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Store</h1>
          <p className="text-muted-foreground text-[13px] mt-1.5">List products for alumni to buy. Delivery is handled by your team outside the platform.</p>
        </div>
        {tab === "Products" && (
          <Button onClick={() => setShowCreate(!showCreate)}>
            <Plus size={15} />Add product
          </Button>
        )}
      </header>

      <div className="flex gap-6 border-b border-border">
        {(["Products", "Orders"] as const).map((t) => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={cn(
              "pb-3 text-[13.5px] font-medium whitespace-nowrap border-b-2 -mb-px transition-colors",
              tab === t ? "text-primary border-primary font-semibold" : "text-muted-foreground border-transparent hover:text-foreground"
            )}
          >
            {t}
          </button>
        ))}
      </div>

      {tab === "Products" && (
        <>
          <Card className="border-border/60">
            <CardContent className="p-4 space-y-2">
              <div className="flex items-center justify-between gap-3 flex-wrap">
                <div>
                  <p className="text-[13px] font-semibold">Default delivery info</p>
                  <p className="text-[12px] text-muted-foreground mt-0.5">Applied to every new product left blank — set it once instead of retyping pickup instructions each time.</p>
                </div>
                {!editingSettings && (
                  <Button size="sm" variant="outline" onClick={() => { setDefaultDeliveryDraft(settings?.defaultDeliveryInfo ?? ""); setEditingSettings(true); }}>
                    {settings?.defaultDeliveryInfo ? "Edit" : "Set default"}
                  </Button>
                )}
              </div>
              {editingSettings ? (
                <div className="space-y-2 pt-1">
                  <Textarea rows={2} value={defaultDeliveryDraft} onChange={(e) => setDefaultDeliveryDraft(e.target.value)} placeholder="Pickup at the alumni office, Mon–Fri 9am–5pm." />
                  <div className="flex gap-2">
                    <Button size="sm" isLoading={settingsMut.isPending} loadingText="Saving" onClick={() => settingsMut.mutate(defaultDeliveryDraft)}>Save</Button>
                    <Button size="sm" variant="outline" onClick={() => setEditingSettings(false)}>Cancel</Button>
                  </div>
                </div>
              ) : (
                <p className="text-[12.5px] text-foreground">{settings?.defaultDeliveryInfo || "Not set — new products need their own delivery info."}</p>
              )}
            </CardContent>
          </Card>

          {showCreate && (
            <ProductForm title="Add a New Product" init={emptyForm} saving={createMut.isPending}
              onSave={(f) => createMut.mutate(f)} onCancel={() => setShowCreate(false)}
              defaultDeliveryInfo={settings?.defaultDeliveryInfo ?? undefined} />
          )}
          {editProduct && (
            <ProductForm
              title={`Edit — ${editProduct.name}`}
              init={{
                name: editProduct.name, description: editProduct.description ?? "",
                price: String(editProduct.price), quantityAvailable: String(editProduct.quantityAvailable),
                deliveryInfo: editProduct.deliveryInfo ?? "", status: editProduct.status,
                images: [], existingImageUrls: editProduct.imageUrls ?? [],
              }}
              saving={updateMut.isPending}
              onSave={(f) => updateMut.mutate({ id: editProduct.id, f })}
              onCancel={() => setEditProduct(null)}
              defaultDeliveryInfo={settings?.defaultDeliveryInfo ?? undefined}
            />
          )}

          <div className="flex items-center gap-2 flex-wrap">
            {["", "Active", "Draft", "Archived"].map((s) => (
              <button
                key={s}
                onClick={() => { setStatusFilter(s); setPage(1); }}
                className={cn(
                  "px-3 py-1.5 rounded-full border text-[12.5px] font-semibold transition-colors",
                  statusFilter === s ? "bg-primary/10 text-primary border-blue-300" : "bg-white text-foreground border-border hover:bg-muted"
                )}
              >
                {s === "" ? "All" : s}
              </button>
            ))}
          </div>

          {isLoading ? (
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-5">
              {Array.from({ length: 6 }).map((_, i) => <CardSkeleton key={i} />)}
            </div>
          ) : products.length === 0 ? (
            <EmptyState icon={<Package size={40} />} title="No products yet" description="Add your first product for alumni to buy." action={<Button onClick={() => setShowCreate(true)}><Plus size={14} />Add product</Button>} />
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-5">
              {products.map((p) => (
                <Card key={p.id} className="flex flex-col overflow-hidden">
                  {p.imageUrls && p.imageUrls.length > 0 ? (
                    <img src={p.imageUrls[0]} alt={p.name} className="w-full h-40 object-cover" />
                  ) : (
                    <div className="w-full h-40 bg-muted/40 flex items-center justify-center">
                      <Package size={28} className="text-muted-foreground" />
                    </div>
                  )}
                  <CardContent className="flex-1 flex flex-col p-4 space-y-2">
                    <div className="flex items-start justify-between gap-2">
                      <h3 className="font-bold text-[14px] leading-snug line-clamp-2">{p.name}</h3>
                      <Badge variant={statusVariant[p.status] ?? "secondary"} size="sm">{p.status}</Badge>
                    </div>
                    <p className="text-[15px] font-bold text-primary">{formatCurrency(p.price)}</p>
                    <p className="text-[12px] text-muted-foreground">{p.quantityAvailable} in stock</p>
                    {p.description && <p className="text-[12px] text-muted-foreground line-clamp-2">{p.description}</p>}
                    <div className="flex items-center gap-2 pt-2 mt-auto border-t border-border/40">
                      <Button size="sm" variant="outline" className="flex-1 h-9 text-[11px] font-bold gap-1" onClick={() => setEditProduct(p)}>
                        <Pencil size={12} />Edit
                      </Button>
                      <Button size="sm" variant="ghost" className="h-9 px-2 text-destructive hover:bg-destructive/10" onClick={() => setDeleteTarget(p)} title="Delete">
                        <Trash2 size={13} />
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
          <Pagination page={page} totalPages={totalPages} onPageChange={setPage} />
        </>
      )}

      {tab === "Orders" && (
        <>
          {ordersLoading ? (
            <div className="space-y-3">
              {Array.from({ length: 5 }).map((_, i) => <CardSkeleton key={i} />)}
            </div>
          ) : orders.length === 0 ? (
            <EmptyState icon={<ShoppingBag size={40} />} title="No paid orders yet" description="Confirmed, fully-paid orders will show up here once members start buying." />
          ) : (
            <div className="space-y-3">
              {orders.map((o) => (
                <Card key={o.id}>
                  <CardContent className="p-4 space-y-2">
                    <div className="flex items-start justify-between gap-2 flex-wrap">
                      <div>
                        <p className="text-[13px] font-bold">{o.memberName ?? o.memberEmail ?? "Member"}</p>
                        <p className="text-[11.5px] text-muted-foreground">{formatDate(o.createdAt)}</p>
                      </div>
                      <div className="flex items-center gap-2">
                        <Badge variant={orderStatusVariant[o.status] ?? "secondary"} size="sm">{o.status}</Badge>
                        <span className="text-[14px] font-bold">{formatCurrency(o.totalAmount)}</span>
                      </div>
                    </div>
                    <div className="divide-y divide-border/40 border-t border-border/40 pt-2">
                      {o.items.map((item, i) => (
                        <div key={i} className="flex items-center justify-between gap-2 py-1.5 text-[12.5px]">
                          <span className="text-foreground">{item.productName} × {item.quantity}</span>
                          <span className="text-muted-foreground">{formatCurrency(item.unitPrice * item.quantity)}</span>
                        </div>
                      ))}
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
          <Pagination page={orderPage} totalPages={orderTotalPages} onPageChange={setOrderPage} />
        </>
      )}

      <ConfirmModal
        open={!!deleteTarget}
        title="Delete Product"
        message={`Delete "${deleteTarget?.name}"? This cannot be undone.`}
        confirmLabel="Delete"
        variant="destructive"
        isLoading={deleteMut.isPending}
        onConfirm={() => deleteTarget && deleteMut.mutate(deleteTarget.id)}
        onCancel={() => setDeleteTarget(null)}
      />
    </div>
  );
}
