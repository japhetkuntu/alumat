import { AdminLayout } from "@/components/admin/admin-layout";

export default function DashboardGroupLayout({ children }: { children: React.ReactNode }) {
  return <AdminLayout>{children}</AdminLayout>;
}
