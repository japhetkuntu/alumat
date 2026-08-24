# Alumni Platform — Frontend Monorepo

Three Next.js apps sharing one component library, managed with **pnpm workspaces** and **Turborepo**.

## Structure

```
apps/
  institution/   Institution Portal — staff-facing admin app for one institution
  member/        Member Portal — alumni-facing app
  platform/      Platform Portal — SaaS operator app across all institutions
packages/
  ui/            @alumni/ui — shared components (Button, Card, Dialog, Table, ...) and utils
```

Each app owns its own brand tokens, pages, and API client. `@alumni/ui` holds only generic, app-agnostic primitives — consumed as raw TypeScript source (no build step), transpiled by each app's own Next.js build via `transpilePackages`.

## Getting started

```bash
pnpm install

pnpm dev:institution   # http://localhost:4100
pnpm dev:member        # http://localhost:3200
pnpm dev:platform      # http://localhost:3300
```

Other tasks run through Turborepo:

```bash
pnpm build:institution   # or build:member / build:platform
pnpm lint:institution    # or lint:member / lint:platform
pnpm typecheck           # runs across every app + packages/ui
```

## Adding a shared component

Add it to `packages/ui/src/components/`, export it from `packages/ui/src/index.ts`, then import it from any app as `import { X } from "@alumni/ui"`. Keep it free of app-specific branding or business logic — those stay local to each app.
