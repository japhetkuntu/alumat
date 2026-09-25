import * as React from "react";

/** Bump when the wording changes. Must match `InstitutionAgreement.CurrentVersion` on the server. */
export const INSTITUTION_AGREEMENT_VERSION = "2026-09-25";
export const INSTITUTION_AGREEMENT_DATE = "25 September 2026";

const heading = "mt-6 mb-2 text-[14.5px] font-semibold";
const para = "mb-3 text-[13.5px] leading-relaxed";

function Clause({ n, title, children }: { n: number; title: string; children: React.ReactNode }) {
  return (
    <section>
      <p className={heading} style={{ color: "var(--foreground)" }}>{n}. {title}</p>
      <div className="space-y-3 text-[13.5px] leading-relaxed" style={{ color: "var(--muted-foreground)" }}>{children}</div>
    </section>
  );
}

/**
 * The Institution Agreement: what an institution agrees to when it sets up a portal. Accepted electronically by
 * someone with authority to act for the institution. Shown on the public agreement page and inside the staff
 * portal when a Super Admin is asked to accept it.
 */
export function InstitutionAgreementBody() {
  return (
    <div>
      <p className={para} style={{ color: "var(--muted-foreground)" }}>
        This agreement is between AlumUnion (&quot;AlumUnion&quot;, &quot;we&quot;, &quot;us&quot;) and the school, association or organization that sets up a
        portal on the platform (the &quot;Institution&quot;, &quot;you&quot;). It sits alongside our Terms of Service and Privacy Policy. If there is a
        conflict about how the Institution uses the platform, this agreement applies.
      </p>

      <Clause n={1} title="Who is agreeing">
        <p>
          The person accepting confirms they are authorised to act for the Institution and to accept this agreement on its
          behalf, and that the details they gave about themselves and the Institution are accurate.
        </p>
      </Clause>

      <Clause n={2} title="What we provide">
        <p>
          We provide a branded portal for your community: a member directory, events, news, jobs, mentorship, forums,
          fundraisers and dues, a store, service requests and related tools, together with an administration console. We set
          up the portal with you, and you decide which features to switch on. We may improve or change features over time.
        </p>
      </Clause>

      <Clause n={3} title="Members&apos; personal data">
        <p>
          For the personal data of your members and staff, the Institution decides why and how it is used and is the
          &quot;data controller&quot;. AlumUnion is the &quot;data processor&quot;: we store and handle that data only to run the portal for you, and
          on your instructions given through the platform. We do not sell it, and we do not use it for our own marketing.
        </p>
        <p>
          As the controller, you are responsible for having a lawful basis and any consent needed to collect and use your
          members&apos; data, for telling members how it is used, for answering their requests to see, correct or delete their data, and for
          any registration or notification duties you have under the Data Protection Act, 2012 (Act 843). We will help with
          those requests through the platform&apos;s tools.
        </p>
        <p>
          We keep the data secure with reasonable technical and organisational measures. We use service providers to run the platform,
          such as hosting, email, SMS and payment providers, and require them to protect the data. If we become aware of a breach
          affecting your members&apos; data, we will tell you without undue delay so you can meet your own duties. When this agreement
          ends, we will return or delete the data on request, except records we must keep by law, such as payment records.
        </p>
      </Clause>

      <Clause n={4} title="Your responsibilities">
        <p>
          You are responsible for what your administrators do in the portal, including who they approve, suspend or remove, what they
          publish, and the events, jobs, news, spotlights and fundraisers they create. Give administrator access only to people you
          trust, and remove it when they leave. Members must be 18 or older. You must use the platform lawfully and keep to our
          Terms of Service.
        </p>
      </Clause>

      <Clause n={5} title="Your name, logo and images">
        <p>
          You confirm you have the right to use the name, logo, crest, colours and any images you upload or ask us to use, and that they
          do not infringe anyone else&apos;s rights. You allow us to display them in your portal and on your behalf in emails and messages the
          platform sends. We will not use your name or logo in our own marketing without your permission, which you can give electronically.
        </p>
      </Clause>

      <Clause n={6} title="Money and payments">
        <p>
          Online payments are handled by a licensed payment provider. AlumUnion does not hold or move your funds. You are responsible for
          the purpose of each fundraiser and dues collection, for spending the money as you tell your members, and for your own accounts.
        </p>
        <p>
          The platform shows totals and records based on payments received through the provider and on entries your administrators make,
          including manual and offline payments. We keep a record of who recorded or changed them, but you are responsible for checking
          them against your provider statements and bank records. We do not guarantee that displayed figures are complete or error-free.
        </p>
      </Clause>

      <Clause n={7} title="Member content and reports">
        <p>
          Members can report posts, mentor profiles, job posts, business listings and spotlights. Reports come to your administrators, who
          decide what to do. We do not review or vet member content, mentors, employers or businesses, and we are not a party to any
          arrangement between members.
        </p>
      </Clause>

      <Clause n={8} title="Fees">
        <p>
          The platform is free for the Institution at the date of this agreement. The payment provider may charge fees on online payments,
          and those are shown where they apply. If we introduce any charge to the Institution, we will give at least 30 days&apos; notice
          before it applies, and you may end this agreement instead.
        </p>
      </Clause>

      <Clause n={9} title="Availability and support">
        <p>
          We work to keep the platform running and to fix problems promptly, but we do not promise it will always be available or
          error-free. We may pause the service briefly for maintenance.
        </p>
      </Clause>

      <Clause n={10} title="Suspension and ending">
        <p>
          Either side may end this agreement at any time by telling the other. We may suspend a portal if it is used unlawfully, puts other
          users or the platform at risk, or breaks this agreement. On request we will help you export your members&apos; data before it is deleted.
        </p>
      </Clause>

      <Clause n={11} title="Liability">
        <p>
          To the fullest extent the law allows, AlumUnion is not liable for indirect or consequential loss, for loss of data or income, or for
          things done by the Institution&apos;s administrators or members. Nothing in this agreement limits liability that cannot lawfully be
          limited.
        </p>
      </Clause>

      <Clause n={12} title="Changes">
        <p>
          We may update this agreement. When we do, we will publish the new version and ask your Super Admin to accept it the next time they
          sign in. Each acceptance is recorded against the version accepted.
        </p>
      </Clause>

      <Clause n={13} title="Governing law">
        <p>
          This agreement is governed by the laws of Ghana. We will try to settle any disagreement by talking first. If we cannot, the courts of
          Ghana have jurisdiction.
        </p>
      </Clause>

      <Clause n={14} title="Accepting electronically">
        <p>
          You accept this agreement by ticking the box and confirming. This is an electronic acceptance under the Electronic Transactions Act,
          2008 (Act 772). We keep a record of who accepted, their stated role, the version, the date and time, and the network address they used.
        </p>
      </Clause>
    </div>
  );
}
