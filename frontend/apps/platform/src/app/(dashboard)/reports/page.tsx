"use client";

import { ReportCenter } from "@alumni/ui";
import { reportCenterApi } from "@/lib/platform-api";
import { handleApiError } from "@/lib/api-client";
import { PageHeading } from "@/components/platform/page-heading";

export default function PlatformReportsPage() {
  return (
    <div className="p-4 sm:p-7 max-w-[1240px]">
      <PageHeading
        title="Reports"
        description="Spreadsheets of institutions, payments and revenue across the platform, prepared in the background and kept for you to download any time."
      />
      <ReportCenter api={reportCenterApi} errorMessage={handleApiError} />
    </div>
  );
}
