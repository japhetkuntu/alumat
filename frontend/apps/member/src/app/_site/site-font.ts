import localFont from "next/font/local";

// Loaded from files that ship with the app, like the portal's own font (see layout.tsx): the build never reaches out to a font service.
export const sora = localFont({
  src: [
    { path: "../../fonts/sora-300.woff2", weight: "300", style: "normal" },
    { path: "../../fonts/sora-400.woff2", weight: "400", style: "normal" },
    { path: "../../fonts/sora-500.woff2", weight: "500", style: "normal" },
    { path: "../../fonts/sora-600.woff2", weight: "600", style: "normal" },
    { path: "../../fonts/sora-700.woff2", weight: "700", style: "normal" },
  ],
  variable: "--site-sora",
  display: "swap",
});
