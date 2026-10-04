"use client";

import { ReportCenter } from "@alumni/ui";
import { reportCenterApi } from "@/lib/institution-api";
import { handleApiError } from "@/lib/api-client";
import { useInstitutionNavTheme } from "@/components/institution/institution-layout";

export default function AdminReportsPage() {
  const { data: navTheme } = useInstitutionNavTheme();

  return (
    <div className="p-4 sm:p-[26px] max-w-[1240px] mx-auto space-y-5">
      <div>
        <h1 className="text-[20px] sm:text-[25px] font-bold m-0">Reports</h1>
        <p className="text-muted-foreground text-[13px] mt-1.5">
          Spreadsheets of your members, dues and payments, prepared in the background and kept for you to download any time.
        </p>
      </div>
      <ReportCenter api={reportCenterApi} errorMessage={handleApiError} hideYearFilters={navTheme?.organizationType === "Community"} />
    </div>
  );
}
