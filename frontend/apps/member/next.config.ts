import type { NextConfig } from "next";
import path from "path";

const nextConfig: NextConfig = {
  output: "standalone",
  // Trace files from the workspace root so the standalone output mirrors the
  // monorepo layout: apps/member/server.js — matching the Dockerfile CMD.
  outputFileTracingRoot: path.join(__dirname, "../../"),
  turbopack: {
    root: "../../",
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
