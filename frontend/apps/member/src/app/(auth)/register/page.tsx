import type { Metadata } from "next";
import { buildEntityMetadata } from "@/lib/entity-og";
import RegisterClient from "./register-client";

/**
 * A community invitation link (`/register?ref=…&community=…`) is pasted into WhatsApp groups, so it needs its own
 * preview card ("Join Mining Engineering 2018 on …") rather than the generic portal title. The client form below is
 * unchanged. Anything else (a plain /register, an unknown community) keeps the default metadata.
 */
export async function generateMetadata({ searchParams }: { searchParams: Promise<{ [key: string]: string | string[] | undefined }> }): Promise<Metadata> {
  const params = await searchParams;
  const community = typeof params.community === "string" ? params.community.trim() : "";
  if (!community || !/^[A-Za-z0-9_-]{1,64}$/.test(community)) return {};
  return buildEntityMetadata("invite", community);
}

export default function RegisterPage() {
  return <RegisterClient />;
}
