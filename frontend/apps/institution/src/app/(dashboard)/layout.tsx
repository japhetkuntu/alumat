import { InstitutionLayout } from "@/components/institution/institution-layout";
import { BatchOptionsProvider } from "@/components/institution/batch-options-provider";

export default function DashboardGroupLayout({ children }: { children: React.ReactNode }) {
  return (
    <InstitutionLayout>
      <BatchOptionsProvider>{children}</BatchOptionsProvider>
    </InstitutionLayout>
  );
}
