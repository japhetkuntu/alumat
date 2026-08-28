import type { NextConfig } from "next";
import path from "path";

const nextConfig: NextConfig = {
  output: "standalone",
  // The dev-only route/build indicator badge (bottom-left corner) — not part
  // of the app UI, never shown in production, but distracting during local dev.
  devIndicators: false,
  // Trace files from the workspace root so the standalone output mirrors the
  // monorepo layout: apps/member/server.js — matching the Dockerfile CMD.
  outputFileTracingRoot: path.join(__dirname, "../../"),
  turbopack: {
    root: "../../",
  },
  // @alumni/ui ships raw TS/TSX source (no build step), so Next must transpile it itself.
  transpilePackages: ["@alumni/ui"],
  // api-client.ts calls same-origin "/api/v1/..." paths (see its comment for
  // why) so nginx can proxy them per-tenant in production. Outside Docker —
  // local `pnpm dev`, no nginx in front — this rewrite plays nginx's role,
  // forwarding those same paths to the local member-api dev port.
  async rewrites() {
    return [
      {
        source: "/api/v1/:path*",
        destination: "http://localhost:5200/api/v1/:path*",
      },
    ];
  },
  images: {
    remotePatterns: [
      {
        protocol: "http",
        hostname: "localhost",
        port: "9000",
        pathname: "/**",
      },
      {
        protocol: "https",
        hostname: "**",
      },
    ],
  },
};

export default nextConfig;
