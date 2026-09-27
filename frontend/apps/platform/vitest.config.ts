import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

// Unit tests for pure helpers under src/lib (no DOM needed).
export default defineConfig({
  resolve: {
    alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) },
  },
  test: {
    include: ["src/**/*.test.ts"],
    environment: "node",
  },
});
