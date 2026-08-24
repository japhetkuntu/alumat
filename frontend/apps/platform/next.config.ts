import type { NextConfig } from "next";
import path from "path";

const nextConfig: NextConfig = {
  output: "standalone",
  // Trace files from the workspace root so the standalone output mirrors the
  // monorepo layout: apps/admin/server.js — matching the Dockerfile CMD.
  outputFileTracingRoot: path.join(__dirname, "../../"),
  turbopack: {
    root: "../../",
  },
  // @alumni/ui ships raw TS/TSX source (no build step), so Next must transpile it itself.
  transpilePackages: ["@alumni/ui"],
};

export default nextConfig;
