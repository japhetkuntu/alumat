FROM node:20-alpine AS base
WORKDIR /app
RUN corepack enable && corepack prepare pnpm@10.17.1 --activate

# ── Install dependencies ──────────────────────────────────────────────────────
FROM base AS deps
# Copy workspace manifests for layer caching
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml turbo.json tsconfig.base.json ./
COPY apps/institution/package.json ./apps/institution/package.json
COPY packages/ui/package.json ./packages/ui/package.json
RUN pnpm install --frozen-lockfile

# ── Build ─────────────────────────────────────────────────────────────────────
FROM base AS builder
COPY --from=deps /app/node_modules ./node_modules
COPY --from=deps /app/apps/institution/node_modules ./apps/institution/node_modules
COPY --from=deps /app/packages/ui/node_modules ./packages/ui/node_modules
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml turbo.json tsconfig.base.json ./
COPY apps/institution ./apps/institution
COPY packages/ui ./packages/ui

ARG NEXT_PUBLIC_INSTITUTION_API_URL
ENV NEXT_PUBLIC_INSTITUTION_API_URL=$NEXT_PUBLIC_INSTITUTION_API_URL
ENV NEXT_TELEMETRY_DISABLED=1

RUN pnpm turbo run build --filter=@alumni/institution

# ── Runtime ───────────────────────────────────────────────────────────────────
FROM node:20-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1

RUN addgroup --system --gid 1001 nodejs
RUN adduser --system --uid 1001 nextjs

COPY --from=builder /app/apps/institution/public ./public
COPY --from=builder --chown=nextjs:nodejs /app/apps/institution/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /app/apps/institution/.next/static ./apps/institution/.next/static

USER nextjs
EXPOSE 3000
ENV PORT=3000
ENV HOSTNAME="0.0.0.0"

CMD ["node", "apps/institution/server.js"]
