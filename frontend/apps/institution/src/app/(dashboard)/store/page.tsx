"use client";

import { useMemo, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Plus, Package, Pencil, Trash2, ShoppingBag, X, ArrowUp, ArrowDown } from "@alumni/ui";
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
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "@alumni/ui";
import { formatCurrency, formatDate } from "@alumni/ui";
import { cn } from "@alumni/ui";
import {
  getStoreProducts, createStoreProduct, updateStoreProduct, deleteStoreProduct, getStoreOrders,
  getStoreSettings, updateStoreSettings, updateStoreOrderDeliveryStatus,
} from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { CardSkeleton } from "@alumni/ui";
import { EmptyState } from "@alumni/ui";
import type { StoreProduct, StoreProductVariant } from "@/types";

const statusVariant: Record<string, "success" | "secondary" | "warning"> = {
  Active: "success",
  Draft: "warning",
  Archived: "secondary",
};

const orderStatusVariant: Record<string, "success" | "secondary" | "warning" | "destructive"> = {
  Successful: "success",
  Pending: "warning",
  Failed: "destructive",
};

const NOT_STARTED = "__not_started__";

interface RowValues {
  sku: string;
  priceOverride: string;
  quantityAvailable: string;
}

const defaultRowValues: RowValues = { sku: "", priceOverride: "", quantityAvailable: "0" };

interface FormState {
  name: string;
  description: string;
  price: string;
  quantityAvailable: string;
  deliveryInfo: string;
  status: string;
  images: File[];
  existingImageUrls: string[];
  optionTypes: string[];
  optionValuesRaw: Record<string, string>;
  /** Keyed by comboKey(optionTypes, combo) — sku/price-override/qty entered per variant combination. */
  variantRowValues: Record<string, RowValues>;
}

const emptyForm: FormState = {
  name: "", description: "", price: "", quantityAvailable: "", deliveryInfo: "", status: "Active",
  images: [], existingImageUrls: [],
  optionTypes: [], optionValuesRaw: {}, variantRowValues: {},
};

function parseValues(raw: string): string[] {
  const seen: string[] = [];
  raw.split(",").map((v) => v.trim()).filter(Boolean).forEach((v) => { if (!seen.includes(v)) seen.push(v); });
  return seen;
}

function cartesianCombos(optionTypes: string[], optionValues: Record<string, string[]>): Record<string, string>[] {
  if (optionTypes.length === 0) return [];
  return optionTypes.reduce<Record<string, string>[]>((acc, type) => {
    const values = optionValues[type] ?? [];
    if (values.length === 0) return [];
    return acc.flatMap((combo) => values.map((v) => ({ ...combo, [type]: v })));
  }, [{}]);
}

function comboKey(optionTypes: string[], options: Record<string, string>): string {
  return optionTypes.map((t) => options[t] ?? "").join("   ");
}

function buildOptionValuesRaw(optionTypes: string[], variants: StoreProductVariant[]): Record<string, string> {
  const raw: Record<string, string> = {};
  optionTypes.forEach((t) => {
    const seen: string[] = [];
    variants.forEach((v) => {
      const val = v.options[t];
      if (val && !seen.includes(val)) seen.push(val);
    });
    raw[t] = seen.join(", ");
  });
  return raw;
}

function ProductForm({ init, onSave, onCancel, saving, title, defaultDeliveryInfo }: {
  init: FormState; onSave: (f: FormState) => void; onCancel: () => void; saving: boolean; title: string; defaultDeliveryInfo?: string;
}) {
  const [form, setForm] = useState(init);
  const [newOptionType, setNewOptionType] = useState("");
  const f = <K extends keyof FormState>(k: K, v: FormState[K]) => setForm((prev) => ({ ...prev, [k]: v }));

  const optionValues = useMemo(
    () => Object.fromEntries(form.optionTypes.map((t) => [t, parseValues(form.optionValuesRaw[t] ?? "")])),
    [form.optionTypes, form.optionValuesRaw]
  );
  const combos = useMemo(() => cartesianCombos(form.optionTypes, optionValues), [form.optionTypes, optionValues]);

  const addOptionType = () => {
    const name = newOptionType.trim();
    if (!name || form.optionTypes.includes(name)) { setNewOptionType(""); return; }
    setForm((prev) => ({ ...prev, optionTypes: [...prev.optionTypes, name], optionValuesRaw: { ...prev.optionValuesRaw, [name]: "" } }));
    setNewOptionType("");
  };

  const removeOptionType = (name: string) => {
    setForm((prev) => {
      const nextRaw = { ...prev.optionValuesRaw };
      delete nextRaw[name];
      return { ...prev, optionTypes: prev.optionTypes.filter((t) => t !== name), optionValuesRaw: nextRaw };
    });
  };

  const updateRow = (key: string, patch: Partial<RowValues>) => setForm((prev) => ({
    ...prev,
    variantRowValues: { ...prev.variantRowValues, [key]: { ...(prev.variantRowValues[key] ?? defaultRowValues), ...patch } },
  }));

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

          <div className="space-y-3 pt-2 border-t border-border/40">
            <div>
              <Label>Variants (optional)</Label>
              <p className="text-[12px] text-muted-foreground mt-0.5">
                Add option types like Size or Color to sell this product in multiple variants. Leave empty to sell it as a single simple product.
              </p>
            </div>
            <div className="flex gap-2">
              <Input
                placeholder="Option type, e.g. Size"
                value={newOptionType}
                onChange={(e) => setNewOptionType(e.target.value)}
                onKeyDown={(e) => { if (e.key === "Enter") { e.preventDefault(); addOptionType(); } }}
              />
              <Button type="button" size="sm" variant="outline" onClick={addOptionType}>Add</Button>
            </div>

            {form.optionTypes.length > 0 && (
              <div className="flex flex-wrap gap-2">
                {form.optionTypes.map((t) => (
                  <span key={t} className="inline-flex items-center gap-1.5 border border-border px-2.5 py-1 text-[12.5px] font-medium">
                    {t}
                    <button type="button" onClick={() => removeOptionType(t)} className="text-muted-foreground hover:text-destructive" aria-label={`Remove ${t}`}>
                      <X size={12} />
                    </button>
                  </span>
                ))}
              </div>
            )}

            {form.optionTypes.map((t) => (
              <div key={t} className="space-y-1">
                <Label className="text-[12.5px]">{t} values</Label>
                <Input
                  placeholder="Comma-separated, e.g. Small, Medium, Large"
                  value={form.optionValuesRaw[t] ?? ""}
                  onChange={(e) => setForm((prev) => ({ ...prev, optionValuesRaw: { ...prev.optionValuesRaw, [t]: e.target.value } }))}
                />
              </div>
            ))}

            {combos.length > 0 && (
              <div className="space-y-2 pt-1">
                <Label className="text-[12.5px]">Variant combinations ({combos.length})</Label>
                <Table>
                  <TableHeader>
                    <TableRow>
                      {form.optionTypes.map((t) => <TableHead key={t}>{t}</TableHead>)}
                      <TableHead>SKU</TableHead>
                      <TableHead>Price override</TableHead>
                      <TableHead>Qty available</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {combos.map((options) => {
                      const key = comboKey(form.optionTypes, options);
                      const row = form.variantRowValues[key] ?? defaultRowValues;
                      return (
                        <TableRow key={key}>
                          {form.optionTypes.map((t) => <TableCell key={t} className="whitespace-nowrap">{options[t]}</TableCell>)}
                          <TableCell>
                            <Input value={row.sku} onChange={(e) => updateRow(key, { sku: e.target.value })} placeholder="Optional" className="h-9 min-w-[7rem]" />
                          </TableCell>
                          <TableCell>
                            <Input type="number" min="0" step="0.01" value={row.priceOverride} onChange={(e) => updateRow(key, { priceOverride: e.target.value })}
                              placeholder="Defaults to product price" className="h-9 min-w-[10rem]" />
                          </TableCell>
                          <TableCell>
                            <Input type="number" min="0" value={row.quantityAvailable} onChange={(e) => updateRow(key, { quantityAvailable: e.target.value })}
                              required className="h-9 min-w-[6rem]" />
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </div>
            )}
          </div>

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
  const deliveryStages = settings?.deliveryStages ?? [];
  const [editingSettings, setEditingSettings] = useState(false);
  const [defaultDeliveryDraft, setDefaultDeliveryDraft] = useState("");
  const [deliveryStagesDraft, setDeliveryStagesDraft] = useState<string[]>([]);
  const settingsMut = useMutation({
    mutationFn: () => updateStoreSettings({
      defaultDeliveryInfo: defaultDeliveryDraft || undefined,
      deliveryStages: deliveryStagesDraft.map((s) => s.trim()).filter(Boolean),
    }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-store-settings"] }); setEditingSettings(false); toast.success("Store settings updated"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const moveStage = (i: number, dir: -1 | 1) => setDeliveryStagesDraft((prev) => {
    const j = i + dir;
    if (j < 0 || j >= prev.length) return prev;
    const next = [...prev];
    [next[i], next[j]] = [next[j], next[i]];
    return next;
  });

  const deliveryStatusMut = useMutation({
    mutationFn: ({ orderId, status }: { orderId: string; status: string | null }) => updateStoreOrderDeliveryStatus(orderId, status),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["admin-store-orders"] }); toast.success("Delivery status updated"); },
    onError: (e) => toast.error(handleApiError(e)),
  });

  const buildVariantFields = (f: FormState) => {
    const optionValues = Object.fromEntries(f.optionTypes.map((t) => [t, parseValues(f.optionValuesRaw[t] ?? "")]));
    const combos = cartesianCombos(f.optionTypes, optionValues);
    const hasVariants = f.optionTypes.length > 0 && combos.length > 0;
    return {
      variantOptionTypes: hasVariants ? f.optionTypes : undefined,
      variants: hasVariants
        ? combos.map((options) => {
            const key = comboKey(f.optionTypes, options);
            const row = f.variantRowValues[key] ?? defaultRowValues;
            return {
              options,
              sku: row.sku.trim() || undefined,
              priceOverride: row.priceOverride.trim() !== "" ? Number(row.priceOverride) : undefined,
              quantityAvailable: Number(row.quantityAvailable) || 0,
            };
          })
        : undefined,
    };
  };

  const createMut = useMutation({
    mutationFn: (f: FormState) => createStoreProduct({
      name: f.name, description: f.description || undefined, price: Number(f.price),
      quantityAvailable: Number(f.quantityAvailable), deliveryInfo: f.deliveryInfo || undefined,
      status: f.status, images: f.images.length > 0 ? f.images : undefined,
      ...buildVariantFields(f),
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
      ...buildVariantFields(f),
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
            <CardContent className="p-4 space-y-4">
              <div className="flex items-center justify-between gap-3 flex-wrap">
                <div>
                  <p className="text-[13px] font-semibold">Store settings</p>
                  <p className="text-[12px] text-muted-foreground mt-0.5">Default delivery info and delivery stages applied across your store.</p>
                </div>
                {!editingSettings && (
                  <Button size="sm" variant="outline" onClick={() => {
                    setDefaultDeliveryDraft(settings?.defaultDeliveryInfo ?? "");
                    setDeliveryStagesDraft(settings?.deliveryStages ?? []);
                    setEditingSettings(true);
                  }}>
                    Edit
                  </Button>
                )}
              </div>

              <div className="space-y-2">
                <p className="text-[12.5px] font-medium">Default delivery info</p>
                {editingSettings ? (
                  <Textarea rows={2} value={defaultDeliveryDraft} onChange={(e) => setDefaultDeliveryDraft(e.target.value)} placeholder="Pickup at the alumni office, Mon–Fri 9am–5pm." />
                ) : (
                  <p className="text-[12.5px] text-foreground">{settings?.defaultDeliveryInfo || "Not set — new products need their own delivery info."}</p>
                )}
              </div>

              <div className="space-y-2 pt-2 border-t border-border/40">
                <div>
                  <p className="text-[12.5px] font-medium">Delivery stages</p>
                  <p className="text-[11.5px] text-muted-foreground mt-0.5">
                    Optional — define stages (e.g. Packed, Shipped, Delivered) to track each order&apos;s fulfillment progress. Leave empty to skip delivery tracking.
                  </p>
                </div>
                {editingSettings ? (
                  <div className="space-y-2">
                    {deliveryStagesDraft.map((stage, i) => (
                      <div key={i} className="flex items-center gap-2">
                        <Input
                          value={stage}
                          onChange={(e) => setDeliveryStagesDraft((prev) => prev.map((s, idx) => (idx === i ? e.target.value : s)))}
                          placeholder={`Stage ${i + 1}`}
                        />
                        <Button type="button" size="sm" variant="ghost" disabled={i === 0} onClick={() => moveStage(i, -1)} title="Move up" aria-label="Move up">
                          <ArrowUp size={13} />
                        </Button>
                        <Button type="button" size="sm" variant="ghost" disabled={i === deliveryStagesDraft.length - 1} onClick={() => moveStage(i, 1)} title="Move down" aria-label="Move down">
                          <ArrowDown size={13} />
                        </Button>
                        <Button type="button" size="sm" variant="ghost" className="text-destructive hover:bg-destructive/10" onClick={() => setDeliveryStagesDraft((prev) => prev.filter((_, idx) => idx !== i))} title="Remove" aria-label="Remove stage">
                          <X size={14} />
                        </Button>
                      </div>
                    ))}
                    <Button type="button" size="sm" variant="outline" onClick={() => setDeliveryStagesDraft((prev) => [...prev, ""])}>
                      <Plus size={13} />Add stage
                    </Button>
                  </div>
                ) : deliveryStages.length > 0 ? (
                  <div className="flex flex-wrap gap-1.5">
                    {deliveryStages.map((s) => <Badge key={s} variant="neutral" size="sm">{s}</Badge>)}
                  </div>
                ) : (
                  <p className="text-[12.5px] text-muted-foreground">Not set — delivery status tracking is off.</p>
                )}
              </div>

              {editingSettings && (
                <div className="flex gap-2">
                  <Button size="sm" isLoading={settingsMut.isPending} loadingText="Saving" onClick={() => settingsMut.mutate()}>Save</Button>
                  <Button size="sm" variant="outline" onClick={() => setEditingSettings(false)}>Cancel</Button>
                </div>
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
                optionTypes: editProduct.variantOptionTypes ?? [],
                optionValuesRaw: buildOptionValuesRaw(editProduct.variantOptionTypes ?? [], editProduct.variants ?? []),
                variantRowValues: Object.fromEntries(
                  (editProduct.variants ?? []).map((v) => [
                    comboKey(editProduct.variantOptionTypes ?? [], v.options),
                    {
                      sku: v.sku ?? "",
                      priceOverride: v.price !== editProduct.price ? String(v.price) : "",
                      quantityAvailable: String(v.quantityAvailable),
                    },
                  ])
                ),
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
                  "px-3 py-1.5 border text-[12.5px] font-semibold transition-colors",
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
                    <p className="text-[12px] text-muted-foreground">
                      {p.quantityAvailable} in stock
                      {p.variants && p.variants.length > 0 && ` · ${p.variants.length} variant${p.variants.length === 1 ? "" : "s"}`}
                    </p>
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
            <EmptyState icon={<ShoppingBag size={40} />} title="No paid orders yet" description="Successful, fully-paid orders will show up here once members start buying." />
          ) : (
            <div className="space-y-3">
              {orders.map((o) => (
                <Card key={o.id}>
                  <CardContent className="p-4 space-y-2">
                    <div className="flex items-start justify-between gap-2 flex-wrap">
                      <div>
                        <p className="text-[13px] font-bold">Order #{o.orderNumber}</p>
                        <p className="text-[11.5px] text-muted-foreground">{o.memberName ?? o.memberEmail ?? "Member"} · {formatDate(o.createdAt)}</p>
                      </div>
                      <div className="flex items-center gap-2">
                        <Badge variant={orderStatusVariant[o.status] ?? "secondary"} size="sm">{o.status}</Badge>
                        <span className="text-[14px] font-bold">{formatCurrency(o.totalAmount)}</span>
                      </div>
                    </div>

                    {deliveryStages.length > 0 && (
                      <div className="flex items-center gap-2 flex-wrap pt-1">
                        <span className="text-[11.5px] font-semibold text-muted-foreground">Delivery status:</span>
                        <FormSelect
                          className="h-9 text-[12.5px] min-w-[9rem]"
                          value={o.deliveryStatus ?? NOT_STARTED}
                          onValueChange={(v) => deliveryStatusMut.mutate({ orderId: o.id, status: v === NOT_STARTED ? null : v })}
                          options={[{ value: NOT_STARTED, label: "Not started" }, ...deliveryStages.map((s) => ({ value: s, label: s }))]}
                        />
                        {o.deliveryStatus && <Badge variant="info" size="sm">{o.deliveryStatus}</Badge>}
                      </div>
                    )}

                    <div className="divide-y divide-border/40 border-t border-border/40 pt-2">
                      {o.items.map((item, i) => (
                        <div key={i} className="flex items-center justify-between gap-2 py-1.5 text-[12.5px]">
                          <span className="text-foreground">
                            {item.productName}
                            {item.variantOptions && Object.keys(item.variantOptions).length > 0 && (
                              <span className="text-muted-foreground"> — {Object.values(item.variantOptions).join(" / ")}</span>
                            )}
                            {" "}× {item.quantity}
                          </span>
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
