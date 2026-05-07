import type { Metadata } from "next";
import "./globals.css";
import { Providers } from "@/components/shared/providers";

export const metadata: Metadata = {
  title: "UMaT Alumni - Member Portal",
  description: "University of Mines and Technology Alumni Member Portal",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body className="min-h-screen bg-background antialiased">
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
