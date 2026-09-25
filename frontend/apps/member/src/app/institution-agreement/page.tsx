import { LegalPageShell } from "@/components/member/legal-page-shell";
import { InstitutionAgreementBody, INSTITUTION_AGREEMENT_DATE } from "@alumni/ui";

export const metadata = {
  title: "Institution Agreement",
  description: "What a school, association or organization agrees to when it sets up a portal on AlumUnion, including how members' personal data is handled.",
};

export default function InstitutionAgreementPage() {
  return (
    <LegalPageShell title="Institution Agreement" effectiveDate={INSTITUTION_AGREEMENT_DATE}>
      <InstitutionAgreementBody />
    </LegalPageShell>
  );
}
