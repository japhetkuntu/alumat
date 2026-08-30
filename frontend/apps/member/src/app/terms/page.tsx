import { LegalPageShell, Section } from "@/components/member/legal-page-shell";

export const metadata = { title: "Terms of Service" };

export default function TermsPage() {
  return (
    <LegalPageShell title="Terms of Service" effectiveDate="25 August 2026">
      <Section heading="1. Who these terms are between">
        <p>
          AlumUnion (&quot;the Platform&quot;, &quot;we&quot;, &quot;us&quot;) is a company registered in Ghana. We provide software
          that lets schools, universities, alumni associations, and other community organizations (each an
          &quot;Institution&quot;) run a branded alumni network for their own members and staff (each a
          &quot;Member&quot;).
        </p>
        <p>
          These Terms apply to everyone who uses the Platform: Institutions that sign up to run a portal, staff
          members who administer one, Members who register on a portal, and guests who make a one-off payment
          through a public fundraiser or membership dues link. By creating an account, logging in, or making a payment through the
          Platform, you agree to these Terms. If you are agreeing on behalf of an Institution, you confirm you
          have the authority to bind that Institution.
        </p>
      </Section>

      <Section heading="2. What the Platform does">
        <p>
          Each Institution gets its own portal — a member-facing site (for example, at a subdomain of
          alumunion.com or the Institution&apos;s own custom domain) and a staff-facing admin console. Depending on
          what the Institution turns on, a portal may include: a searchable alumni directory, an opt-in world
          map showing where alumni are based, class notes and forum discussions, events with RSVP, a job board,
          mentorship matching, news and announcements, membership renewal, and fundraisers or membership dues
          collected online (through our payment partner, Paystack) or recorded manually by staff.
        </p>
        <p>
          Institutions control which of these features are switched on for their own portal, and are responsible
          for the content, events, and fundraisers they publish. We provide the software and infrastructure; we do
          not review or endorse an Institution&apos;s content before it is published.
        </p>
      </Section>

      <Section heading="3. Eligibility and accounts">
        <p>
          You must be at least 18 years old to create a Member account. Member registration is intended for
          alumni, students, and staff of a participating Institution — an Institution may ask for a graduation
          year, student ID, or department to confirm you belong to their community, and may require a staff
          member to approve new registrations before an account becomes active.
        </p>
        <p>
          You are responsible for keeping your login credentials confidential and for everything that happens
          under your account. Tell us immediately if you believe your account has been accessed without your
          permission. You must provide accurate information when you register and keep your profile reasonably
          up to date.
        </p>
      </Section>

      <Section heading="4. Guest payments">
        <p>
          Some fundraiser or membership dues links can be paid without creating an account (&quot;guest payment&quot;). If you pay as a
          guest through a link that was shared by a registered Member, the contribution may be recorded against
          that Member&apos;s account for the Institution&apos;s records, but is clearly marked as paid by a guest — it is
          never shown as if the sharer paid it themselves. If you are logged in when you pay, the payment is
          always recorded against your own account, even on a link someone else shared with you.
        </p>
      </Section>

      <Section heading="5. Acceptable use">
        <p>You agree not to use the Platform to:</p>
        <ul className="list-disc pl-5 space-y-1.5">
          <li>Impersonate another person, or misrepresent your affiliation with an Institution;</li>
          <li>Post content that is unlawful, harassing, defamatory, or that infringes someone else&apos;s rights;</li>
          <li>Attempt to access another Member&apos;s account, or data you are not authorised to see;</li>
          <li>Scrape, harvest, or export Member contact details for purposes outside the Platform, including
            unsolicited marketing;</li>
          <li>Interfere with the normal operation of the Platform, including by introducing malware or
            attempting to bypass rate limits, authentication, or feature restrictions;</li>
          <li>Use the payment or fundraiser features to collect money for a purpose other than the one stated.</li>
        </ul>
        <p>
          Content you post in forums, class notes, communities, or your profile (&quot;User Content&quot;) remains
          yours, but you give the Platform and the relevant Institution a licence to store, display, and
          distribute it within that Institution&apos;s portal so the features you used it for can work. Institution
          staff can remove User Content or suspend accounts that breach these Terms or the Institution&apos;s own
          community guidelines.
        </p>
      </Section>

      <Section heading="6. Payments">
        <p>
          Online payments (membership dues, fundraiser contributions) are processed by Paystack, a licensed
          payment service provider. We do not receive or store your full card or mobile money PIN details —
          Paystack handles that directly. A processing fee, disclosed at checkout, may apply on top of the
          amount you choose to pay. Amounts already paid are generally non-refundable except where required by
          law or at an Institution&apos;s discretion; refund requests should go to the Institution that ran the
          fundraiser or dues cycle, since they hold the funds and the record of what was collected.
        </p>
        <p>
          Institutions set their own fundraiser amounts, deadlines, and (for membership) yearly dues. We are not a
          party to the underlying reason a payment is being collected — that relationship is between you and the
          Institution.
        </p>
      </Section>

      <Section heading="7. Notifications">
        <p>
          With your consent, an Institution can reach you by email or SMS for things like fundraiser reminders,
          event updates, or membership renewal notices, using the phone number and preferences you provide in
          your profile. You can turn SMS notifications off at any time from your notification settings; some
          account-critical emails (like password resets or payment receipts) cannot be turned off, since they
          exist to protect your account and give you a record of transactions. We may add further channels,
          such as WhatsApp, in the future — this policy will be updated before that happens.
        </p>
      </Section>

      <Section heading="8. Alumni Map">
        <p>
          The Alumni Map plots Members by general location (city/country as you entered it) so fellow alumni can
          see where the community is based. It is opt-in and off by default — your location is never shown on
          the map unless you turn the setting on yourself in your profile, and you can turn it back off at any
          time, which removes you from the map immediately.
        </p>
      </Section>

      <Section heading="9. Suspension and termination">
        <p>
          An Institution can suspend or remove a Member account from its own portal (for example, for breaching
          community guidelines, or because someone is confirmed not to be an alumnus). We can suspend or
          terminate access to the Platform for an Institution or a Member who breaches these Terms, engages in
          fraud, or creates security or legal risk for the Platform or other users. You may stop using the
          Platform, or ask an Institution to delete your Member account, at any time.
        </p>
      </Section>

      <Section heading="10. Disclaimers and limitation of liability">
        <p>
          The Platform is provided on an &quot;as is&quot; and &quot;as available&quot; basis. We work to keep it reliable and
          secure, but we do not guarantee it will be uninterrupted or error-free. We are not responsible for the
          accuracy of content an Institution or its Members post, or for disputes between an Institution and its
          Members (for example, over how collected fundraiser or dues funds are used).
        </p>
        <p>
          To the fullest extent permitted by Ghanaian law, our total liability to you arising out of your use of
          the Platform is limited to the amount you paid to us directly (if any) in the twelve months before the
          claim arose. Nothing in these Terms limits liability for fraud, or for death or personal injury caused
          by negligence, where such limitation is not permitted by law.
        </p>
      </Section>

      <Section heading="11. Changes to these Terms">
        <p>
          We may update these Terms as the Platform evolves. If we make a material change, we will update the
          effective date above and, where practical, let Institutions and Members know in advance through the
          Platform. Continuing to use the Platform after a change takes effect means you accept the updated
          Terms.
        </p>
      </Section>

      <Section heading="12. Governing law">
        <p>
          These Terms are governed by the laws of the Republic of Ghana. Any dispute arising from these Terms or
          your use of the Platform that cannot be resolved informally will be subject to the exclusive
          jurisdiction of the courts of Ghana.
        </p>
      </Section>

      <Section heading="13. Contact">
        <p>
          Questions about these Terms can be sent to{" "}
          <a href="mailto:support@alumunion.com" className="font-semibold underline underline-offset-2" style={{ color: "var(--primary)" }}>
            support@alumunion.com
          </a>. For anything specific to your own Institution&apos;s portal — like membership dues or event
          details — the fastest route is usually your Institution&apos;s own alumni office, since they run that
          side of things directly.
        </p>
      </Section>
    </LegalPageShell>
  );
}
