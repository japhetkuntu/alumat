# Deployment

## The one way to deploy

```bash
# 1. Push to main. GitHub builds what changed (watch it in the Actions tab, workflow "Build").
# 2. On the server:
sudo bash /opt/alumunion-src/deploy/pull-deploy.sh --wait
```

That is all. It waits for GitHub to finish, downloads only the services that changed and restarts just those. If a
service does not come up healthy with the new build, it is put back to the version it had before and the script says so.

| I want to... | Run |
| --- | --- |
| Deploy what I just pushed | `sudo bash /opt/alumunion-src/deploy/pull-deploy.sh --wait` |
| See what is live and what is new, without changing anything | `sudo bash /opt/alumunion-src/deploy/pull-deploy.sh --status` |
| Deploy only some services | `sudo bash /opt/alumunion-src/deploy/pull-deploy.sh member-api frontend-member` |
| Roll back to an older version | Actions, **Build**, Run workflow on the older commit (tick "Rebuild every service"), then `pull-deploy.sh --wait` |

Rules that keep it safe:

- **One way only.** The old `deploy.sh` (compiling on the server) now refuses to run unless you set `ALLOW_SERVER_BUILD=1`.
  It does not leave the marker `pull-deploy.sh` compares, so using it would let the next pull put an older build back.
- **One deploy at a time.** A second run while one is going stops with a message.
- **A broken build never replaces a good one silently.** A failed build on GitHub publishes nothing, so the server
  keeps what it has. A build that starts badly on the server is rolled back automatically (database changes that the new
  version already made are not undone, so keep migrations backward compatible).
- **After a deploy that changes `nginx` templates**, apply it once by hand: `sudo bash /opt/alumunion-src/deploy/update-nginx-csp.sh` (resource previews) and `sudo bash /opt/alumunion-src/deploy/update-nginx-integrations.sh` (lets a partner's server call the status check without the per-IP request limit). It backs up, tests with `nginx -t`, and only then reloads.
- **Server settings stay on the server.** Nginx, the systemd unit files and `/etc/alumunion/*.env` are changed by hand
  there, as before. Database migrations still run when the APIs start.

The sections below explain how it works, the one-time setup, and the old build-on-the-server path.

---

## How the fast deploy works and how to set it up

Building on the server is the slow part: four .NET services and three Next.js apps compiled one after another on a
small machine. The **Build** workflow (`.github/workflows/build.yml`) moves that to GitHub, and the server just pulls
the finished builds:

1. You push to `main`. GitHub builds only the services the change affects (same rules as `deploy.sh`), **in parallel**.
2. It stores the finished builds in this repository, on a separate branch, `deploy-builds`. `main` stays your development
   branch and the server deploys from `deploy-builds`. Each run replaces that branch with a single commit, so it does
   not grow, and unchanged services carry over untouched. Because there is only ever one commit, to roll back you run
   the Build workflow again on an older commit (Actions, Build, Run workflow, choose the branch or tag).
3. On the server you run `pull-deploy.sh`. It downloads only what changed, then stops, swaps and starts just those
   services. Nothing is compiled on the server.

```bash
sudo bash /opt/alumunion-src/deploy/pull-deploy.sh --wait    # just pushed: waits for GitHub to finish, then deploys
sudo bash /opt/alumunion-src/deploy/pull-deploy.sh           # deploy whatever is already built
sudo bash /opt/alumunion-src/deploy/pull-deploy.sh member-api frontend-member   # only these
```

Expect a few minutes of building on GitHub (in parallel) and well under a minute on the server, whatever its size. No
SSH keys or secrets are needed: the server fetches with the same repository access it already uses for `git pull`.

### One-time setup

1. **Public build settings, in the repository (nothing to add on GitHub).** The frontends bake their `NEXT_PUBLIC_*` values
   into the browser bundle when they are built, and GitHub cannot read the server's env files, so the same values are kept
   in `deploy/public-env/member.env`, `institution.env` and `platform.env`. They are public by definition (every visitor
   downloads them), so they are safe to commit. On the server, show what to copy:

   ```bash
   for a in member institution platform; do echo "--- $a"; grep '^NEXT_PUBLIC_' /opt/alumunion-src/frontend/apps/$a/.env.production.local; done
   ```

   Put those lines in the matching file, replacing `CHANGE_ME`, and push. The Build fails with a clear message while
   `CHANGE_ME` is still there, so a wrong value can never be published. Copy only the `NEXT_PUBLIC_` lines.
2. **Settings, Actions, General, Workflow permissions:** allow the workflow to write to the repository ("Read and write
   permissions"). The Build workflow needs it to store the builds.
3. **Push this change, let the first Build run finish** (it builds everything, because there are no earlier builds),
   then on the server:

   ```bash
   sudo git -C /opt/alumunion-src pull --ff-only
   sudo bash /opt/alumunion-src/deploy/pull-deploy.sh
   ```

   The first run replaces every service once; after that only changed services are replaced.

You can also run the **Build** workflow by hand from the Actions tab, optionally forcing every service.

**Keep the builds out of your laptop.** `deploy-builds` is a normal branch, so `git fetch` on a laptop would download it
(a few hundred MB). Tell git to skip it once per clone:

```bash
git config --add remote.origin.fetch '^refs/heads/deploy-builds'
```

### Good to know

- `deploy.sh` (build on the server) is now a guarded fallback, so there is no way to mix the two by accident. See the
  Legacy section below.
- Changes to Nginx, the systemd unit files or the server's `/etc/alumunion/*.env` files are still applied by hand on
  the server. Database migrations still run when the APIs start.
- If a build fails on GitHub, nothing is published and the server keeps what it has. Each service records the commit it
  was built from (`.built-sha`), so the next run catches up whatever was missed.

---

# Legacy: building on the server (fallback only)

Everything below is the old way, which compiles on the server. It is kept only for when GitHub is unavailable, and it
refuses to run unless you set `ALLOW_SERVER_BUILD=1`, for example
`sudo env ALLOW_SERVER_BUILD=1 bash /opt/alumunion-src/deploy/deploy.sh member-api`. Afterwards, once GitHub is back,
run a full **Build** (tick "Rebuild every service") and `pull-deploy.sh` so the server and GitHub agree again.

The systemd deployment script (run with `ALLOW_SERVER_BUILD=1`, see above):

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

## Keeping server builds fast

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

These targets and cache improvements apply to `deploy/deploy.sh`.

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
