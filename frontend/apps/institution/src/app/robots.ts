import type { MetadataRoute } from "next";

// Institution-staff backoffice — nothing here is meant for search engines.
export default function robots(): MetadataRoute.Robots {
  return {
    rules: { userAgent: "*", disallow: "/" },
  };
}
