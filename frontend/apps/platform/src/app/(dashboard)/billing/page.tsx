"use client";

import { useQuery } from "@tanstack/react-query";
import { Card, CardContent } from "@alumni/ui";
import { Badge } from "@alumni/ui";
import { Table, TableBody, TableCell, TableEmpty, TableHead, TableHeader, TableRow } from "@alumni/ui";
import { formatCurrency } from "@alumni/ui";
import { getInstitutions } from "@/lib/platform-api";

export default function BillingPage() {
  const { data } = useQuery({
    queryKey: ["institutions", { page: 1, pageSize: 200 }],
    queryFn: () => getInstitutions({ page: 1, pageSize: 200 }),
  });
  const institutions = data?.results ?? [];

  const activeInstitutions = institutions.filter((i) => i.status === "Active");
  const trialCount = institutions.filter((i) => i.status === "Trial").length;
  const totalMrr = activeInstitutions.reduce((s, i) => s + i.mrr, 0);

  return (
    <div className="p-7 max-w-[1500px]">
      <h1 className="text-[24px] font-bold">Subscriptions &amp; Billing</h1>
      <p className="text-muted-foreground text-[13px] mt-1 mb-6">Platform-wide revenue operations across every institution.</p>

      <div className="grid grid-cols-2 lg:grid-cols-3 gap-4 mb-5">
        <Card><CardContent className="p-5"><p className="text-[12px] text-muted-foreground">Active subscriptions</p><p className="text-[24px] font-bold mt-1">{activeInstitutions.length}</p></CardContent></Card>
        <Card><CardContent className="p-5"><p className="text-[12px] text-muted-foreground">Trial institutions</p><p className="text-[24px] font-bold mt-1">{trialCount}</p></CardContent></Card>
        <Card><CardContent className="p-5"><p className="text-[12px] text-muted-foreground">Total MRR</p><p className="text-[24px] font-bold mt-1">{formatCurrency(totalMrr, "USD")}</p></CardContent></Card>
      </div>

      <Card>
        <div className="px-5 py-4 border-b border-border"><p className="text-[14px] font-semibold">All subscriptions</p></div>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Institution</TableHead>
              <TableHead>Plan</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>MRR</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {institutions.length === 0 && <TableEmpty title="No institutions yet" colSpan={4} />}
            {institutions.map((inst) => (
              <TableRow key={inst.id}>
                <TableCell className="font-semibold">{inst.name}</TableCell>
                <TableCell>{inst.plan}</TableCell>
                <TableCell>
                  <Badge variant={inst.status === "Suspended" ? "destructive" : inst.status === "Trial" ? "info" : "success"}>
                    {inst.status}
                  </Badge>
                </TableCell>
                <TableCell>{formatCurrency(inst.mrr, "USD")}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>
    </div>
  );
}
