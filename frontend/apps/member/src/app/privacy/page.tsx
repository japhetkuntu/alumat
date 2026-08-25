import { LegalPageShell, Section } from "@/components/member/legal-page-shell";

export const metadata = { title: "Privacy Policy" };

export default function PrivacyPage() {
  return (
    <LegalPageShell title="Privacy Policy" effectiveDate="25 August 2026">
      <Section heading="1. Scope of this policy">
        <p>
          This policy explains how AlumUnion [Legal entity name to be inserted], operating the Alumni Portal
          platform (&quot;we&quot;, &quot;us&quot;), handles personal data when a school, university, or other institution
          (&quot;Institution&quot;) uses the Platform to run an alumni network, and when their alumni, students, or
          staff (&quot;Members&quot;) use that network. It is written to meet Ghana&apos;s Data Protection Act, 2012
          (Act 843), and applies to every Institution portal hosted on the Platform.
        </p>
        <p>
          In most cases, the Institution decides why and how Members&apos; data is collected — for example, which
          fields are required at registration, or which campaigns are run — which makes the Institution the
          data controller for its own Members&apos; records. We act as the data processor that stores and runs
          that data on the Institution&apos;s behalf, and we are separately the controller for platform-level
          records like billing and security logs. If you are unsure which applies to a specific piece of data,
          contact us and we will clarify.
        </p>
      </Section>

      <Section heading="2. What we collect">
        <p><strong style={{ color: "var(--foreground)" }}>From Members, when you register or use a portal:</strong></p>
        <ul className="list-disc pl-5 space-y-1.5">
          <li>Identity and contact details — full name, email address, mobile number, graduation year, student
            ID, department;</li>
          <li>Profile details you choose to add — company, job title, location, LinkedIn URL, bio, profile
            photo;</li>
          <li>Location, only if you turn on the Alumni Map — the city/country you entered, nothing more precise;</li>
          <li>Activity data — event RSVPs, forum and class-note posts, job applications, mentorship requests,
            community memberships;</li>
          <li>Payment records — the amount, date, campaign, and status of a contribution or membership payment.
            We do not receive or store your card number, CVV, or mobile money PIN; those go directly to
            Paystack, our payment processor;</li>
          <li>Notification preferences — whether you&apos;ve opted in to SMS alerts, and the number they&apos;re sent
            to;</li>
          <li>Technical data — IP address, browser/device type, and basic usage logs, collected automatically for
            security and to keep the service running.</li>
        </ul>
        <p><strong style={{ color: "var(--foreground)" }}>From Institution staff:</strong> name, work email, and role,
          used to manage staff access to the admin console.</p>
        <p><strong style={{ color: "var(--foreground)" }}>From guest payers:</strong> if you pay a campaign without
          an account, we collect the payment amount, reference, and — only if you choose to provide one — an
          email address to receive a receipt.</p>
      </Section>

      <Section heading="3. Why we process it">
        <p>Under Act 843, we rely on the following legal bases:</p>
        <ul className="list-disc pl-5 space-y-1.5">
          <li><strong style={{ color: "var(--foreground)" }}>Performance of a contract</strong> — creating and
            running your account, showing your directory listing to fellow Members, processing a payment you
            initiate, sending you the account emails needed to operate the service (like password resets and
            payment receipts);</li>
          <li><strong style={{ color: "var(--foreground)" }}>Consent</strong> — showing your location on the
            Alumni Map, sending you SMS notifications, and any marketing communications. You can withdraw this
            consent at any time from your profile settings, and we stop that specific use going forward;</li>
          <li><strong style={{ color: "var(--foreground)" }}>Legitimate interest</strong> — keeping the Platform
            secure (fraud prevention, abuse detection), improving reliability, and maintaining audit logs of
            staff actions on an Institution&apos;s portal;</li>
          <li><strong style={{ color: "var(--foreground)" }}>Legal obligation</strong> — retaining payment and
            transaction records for the period required under Ghanaian financial and tax record-keeping rules.</li>
        </ul>
      </Section>

      <Section heading="4. Who we share it with">
        <p>We share personal data only where it is needed to run the Platform, and always under a contract that
          requires the recipient to protect it:</p>
        <ul className="list-disc pl-5 space-y-1.5">
          <li><strong style={{ color: "var(--foreground)" }}>Paystack</strong> — processes online payments; they
            receive what is needed to complete a transaction (amount, email, payment method details you enter
            directly with them);</li>
          <li><strong style={{ color: "var(--foreground)" }}>Arkesel</strong> — delivers SMS notifications when
            you have opted in; they receive your phone number and the message content, nothing more. (We also
            integrate with a WhatsApp provider for a future release — it is not active during the current
            pilot, and this policy will be updated before it is turned on.)</li>
          <li><strong style={{ color: "var(--foreground)" }}>Cloud hosting and storage providers</strong>{" "}
            (including DigitalOcean) — store the Platform&apos;s database and uploaded files (like profile photos
            and campaign banners) securely;</li>
          <li><strong style={{ color: "var(--foreground)" }}>Your own Institution&apos;s staff</strong> — the staff
            of the Institution whose portal you registered on can see the profile and activity data needed to
            run that portal (for example, to approve your membership or confirm a manual payment). Staff at one
            Institution cannot see another Institution&apos;s Members;</li>
          <li><strong style={{ color: "var(--foreground)" }}>Other Members</strong> — your directory listing, and
            anything you post in forums, class notes, or communities, is visible to fellow Members of the same
            Institution, per the visibility settings that feature offers;</li>
          <li>
            <strong style={{ color: "var(--foreground)" }}>Law enforcement or regulators</strong> — only where we
            are legally required to disclose it.
          </li>
        </ul>
        <p>We do not sell personal data, and we do not share it with third parties for their own marketing.</p>
      </Section>

      <Section heading="5. Where data is stored">
        <p>
          Platform data is hosted with cloud infrastructure providers that may process data outside Ghana. Where
          that happens, we rely on our processors&apos; contractual security commitments and, where required by
          Act 843, appropriate safeguards for the transfer.
        </p>
      </Section>

      <Section heading="6. How long we keep it">
        <p>
          We keep Member account data for as long as the account is active on an Institution&apos;s portal. If an
          Institution deletes a Member&apos;s account, we delete the associated personal data within a reasonable
          period, except where we are required to retain payment or transaction records for longer under
          Ghanaian financial record-keeping obligations, or where data has already been anonymised or aggregated
          in a way that no longer identifies you (for example, in overall giving statistics).
        </p>
      </Section>

      <Section heading="7. Your rights">
        <p>Under the Data Protection Act, 2012 (Act 843), you have the right to:</p>
        <ul className="list-disc pl-5 space-y-1.5">
          <li>Know what personal data we hold about you and how it is used;</li>
          <li>Access a copy of your personal data;</li>
          <li>Correct inaccurate or incomplete data — most profile fields can be edited directly from your
            account;</li>
          <li>Object to, or ask us to stop, a specific processing activity (for example, withdrawing your Alumni
            Map or notification consent);</li>
          <li>Ask for your account and associated personal data to be deleted, subject to the retention
            exceptions described above;</li>
          <li>Lodge a complaint with Ghana&apos;s Data Protection Commission if you believe your data has been
            mishandled.</li>
        </ul>
        <p>
          To exercise any of these rights, start with your Institution&apos;s alumni office if the request relates
          to your Member account, since they control that data day to day. For anything platform-level, or if
          your Institution is unresponsive, contact us directly at{" "}
          <a href="mailto:privacy@alumunion.com" className="font-semibold underline underline-offset-2" style={{ color: "var(--primary)" }}>
            privacy@alumunion.com
          </a>.
        </p>
      </Section>

      <Section heading="8. Security">
        <p>
          We use industry-standard measures to protect personal data, including encrypted connections (HTTPS),
          hashed passwords (we never store your password in readable form), role-based access so staff only see
          what their role requires, and tenant isolation so one Institution cannot access another&apos;s data. No
          system is completely immune to risk, and we will notify affected Institutions and, where required by
          law, the Data Protection Commission, in the event of a data breach likely to affect your rights.
        </p>
      </Section>

      <Section heading="9. Cookies and similar technology">
        <p>
          We use strictly necessary cookies/local storage to keep you signed in and remember basic preferences
          (like your institution workspace during development). We do not currently use third-party advertising
          or tracking cookies.
        </p>
      </Section>

      <Section heading="10. Children">
        <p>
          The Platform is built for alumni, students, and staff of participating Institutions and is not
          intended for children under 18. If we learn that we have collected personal data from a child without
          appropriate consent, we will delete it.
        </p>
      </Section>

      <Section heading="11. Changes to this policy">
        <p>
          We may update this policy as the Platform changes. Material changes will be reflected in the effective
          date above, and where practical we will let Institutions and Members know in advance.
        </p>
      </Section>

      <Section heading="12. Contact">
        <p>
          For any question about this policy or how your data is handled, email{" "}
          <a href="mailto:privacy@alumunion.com" className="font-semibold underline underline-offset-2" style={{ color: "var(--primary)" }}>
            privacy@alumunion.com
          </a>.
        </p>
      </Section>
    </LegalPageShell>
  );
}
