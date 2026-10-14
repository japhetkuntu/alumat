# Deployment

Run the systemd deployment script on the server:

```bash
sudo bash /opt/alumunion-src/deploy/deploy.sh backend
```

`backend` builds and deploys the institution API, member API, platform API and
Operations worker without installing or building any frontend. For a change to
one service, use its target instead:

```bash
sudo bash /opt/alumunion-src/deploy/deploy.sh member-api
```

With no target, `all` checks changes since the last successful full deployment
and selects the affected services and frontends. Shared backend changes select
all four .NET services. A first full deployment builds everything.

## Keeping deployments fast

Keep `bin`, `obj` and NuGet caches between deployments. The script checks free
disk space but does not automatically delete these caches. Previously it deleted
all backend build outputs whenever less than 5 GB was available with the default
settings, forcing otherwise incremental builds to start cold.

Use `CLEAN_BUILD=1` only to troubleshoot stale build outputs:

```bash
sudo env CLEAN_BUILD=1 bash /opt/alumunion-src/deploy/deploy.sh backend
```

Builds normally run while the existing services serve traffic, and stop services
only for the final copy and restart. If the server is running out of memory and
swapping during compilation, `STOP_FIRST=1` can free the selected services' memory,
at the cost of downtime for the entire build:

```bash
sudo env STOP_FIRST=1 bash /opt/alumunion-src/deploy/deploy.sh backend
```

Each step prints elapsed time and memory/swap usage. Compare the same target on
successive runs to assess warm-build performance. A cold build, pending database
migrations, server load and package downloads can still extend deployment time.
There is no measured speedup until this version has been run on the server.

Backend builds use at most two MSBuild workers, or one when Linux reports less
than 2 GB available memory. This limits competing compiler processes on a busy
droplet. Override with `DOTNET_BUILD_JOBS=1` (or a larger positive integer for a
larger server). Compiler-server sharing and MSBuild node reuse are disabled for
these deployment builds so they do not leave reusable build processes behind.
This can reduce memory pressure; it is not a guaranteed speedup on every server.

Restore and compile/publish now have separate timings. Publish also prints an
MSBuild performance summary. If a deployment still stalls, capture that output
alongside `free -h` and `nproc` on the server: it distinguishes package/network
waits, expensive compilation and a machine that lacks memory. Compilation checks
and analyzers remain enabled; deployment does not run unit tests.

The root `deploy.sh` is the older Docker Compose deployment path. These targets
and cache improvements apply to `deploy/deploy.sh`.

## Frontend deployments

The individual frontend targets remain available:

```bash
sudo bash /opt/alumunion-src/deploy/deploy.sh frontend-member
sudo bash /opt/alumunion-src/deploy/deploy.sh frontend-institution
sudo bash /opt/alumunion-src/deploy/deploy.sh frontend-platform
```

Frontend builds now use the workspace's Turborepo cache. Keep `frontend/.turbo`
and each app's `.next/cache` between deployments. An identical build can restore
its finished output; a changed app still builds, using Next.js's compiler cache.
Environment files and the public/internal API environment variables participate
in the build cache key so changing production settings invalidates cached output.
Apps build sequentially to limit memory pressure on the server. Dependency
installation prefers packages already in the local pnpm store, with the frozen
lockfile still enforced.

The standalone copy protects public files and static assets from deletion before
their own synchronization passes. Ownership is set during copying, avoiding a
separate recursive ownership pass over the entire frontend tree.

To force a frontend build without reading the Turborepo cache:

```bash
sudo env TURBO_FORCE=true bash /opt/alumunion-src/deploy/deploy.sh frontend-member
```
