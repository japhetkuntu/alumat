import { ImageResponse } from "next/og";
import { getInstitutionTheme } from "@/lib/theme";
import { SITE_NAME } from "@/lib/seo";

// See layout.tsx's `dynamic` export comment — this route reads headers() on
// every request (via getInstitutionTheme) and can never be static.
export const dynamic = "force-dynamic";

export const alt = "AlumUnion — free alumni portal software";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

// Generated per-request rather than a static asset because the image itself
// is tenant-branded: an institution's link preview shows their own name and
// brand color, the bare marketing domain shows the product pitch.
export default async function OgImage() {
  const theme = await getInstitutionTheme();
  const name = theme?.displayName || theme?.portalName;
  const primary = theme?.primaryColorHex || "#2563EB";
  const heading = name ? `${name} Alumni Portal` : "Free Alumni Portal Software";
  const sub = name ? `Powered by ${SITE_NAME}` : "Directory · Events · Dues · Jobs · Mentorship";

  return new ImageResponse(
    (
      <div
        style={{
          height: "100%",
          width: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "center",
          alignItems: "flex-start",
          padding: "80px",
          background: primary,
          color: "#fff",
          fontFamily: "sans-serif",
        }}
      >
        <div style={{ display: "flex", fontSize: 28, fontWeight: 600, letterSpacing: 2, textTransform: "uppercase", opacity: 0.75, marginBottom: 24 }}>
          {SITE_NAME}
        </div>
        <div style={{ display: "flex", fontSize: 64, fontWeight: 700, lineHeight: 1.15, maxWidth: 980 }}>{heading}</div>
        <div style={{ display: "flex", fontSize: 30, marginTop: 28, opacity: 0.85 }}>{sub}</div>
      </div>
    ),
    { ...size }
  );
}
