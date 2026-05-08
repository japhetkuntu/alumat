FROM node:20-alpine AS base
WORKDIR /app

# ── Install dependencies ──────────────────────────────────────────────────────
FROM base AS deps
# Copy workspace manifests for layer caching
COPY package.json package-lock.json ./
COPY apps/member/package.json ./apps/member/package.json
RUN npm ci

# ── Build ─────────────────────────────────────────────────────────────────────
FROM base AS builder
COPY --from=deps /app/node_modules ./node_modules
# Root package.json needed for workspace build scripts
COPY package.json ./package.json
COPY apps/member ./apps/member

# Ensure the app workspace can resolve next from the hoisted root node_modules.
RUN mkdir -p /app/apps/member/node_modules && ln -s /app/node_modules /app/apps/member/node_modules

ARG NEXT_PUBLIC_MEMBER_API_URL
ENV NEXT_PUBLIC_MEMBER_API_URL=$NEXT_PUBLIC_MEMBER_API_URL
ENV NEXT_TELEMETRY_DISABLED=1

# Run from workspace root so Turbopack can resolve next from /app/node_modules
RUN npm run build:member

# ── Runtime ───────────────────────────────────────────────────────────────────
FROM node:20-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1

RUN addgroup --system --gid 1001 nodejs
RUN adduser --system --uid 1001 nextjs

COPY --from=builder /app/apps/member/public ./public
COPY --from=builder --chown=nextjs:nodejs /app/apps/member/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /app/apps/member/.next/static ./.next/static

USER nextjs
EXPOSE 3000
ENV PORT=3000
ENV HOSTNAME="0.0.0.0"

CMD ["node", "apps/member/server.js"]
