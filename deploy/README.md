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

## Fast deploys from GitHub (recommended)

Building on the server is the slow part: four .NET services and three Next.js apps compiled one after another on a
small machine. `.github/workflows/deploy.yml` moves the building to GitHub. On every push to `main` it works out what
changed (same rules as `deploy.sh`), builds those services **in parallel** on GitHub's runners, copies the finished
builds to the server (rsync sends only what differs from the last build) and runs `deploy/activate.sh`, which stops the
changed services, swaps the files in and starts them. Nothing is compiled on the server, so a deploy is typically a few
minutes of building on GitHub plus well under a minute on the server, whatever the server's size.

You can also run it by hand from the Actions tab (**Deploy**, Run workflow), optionally forcing everything.

### One-time setup

1. **A deploy user on the server.** It only needs to copy files into the staging folder and run one script as root:

   ```bash
   sudo adduser --disabled-password --gecos "" deploy
   sudo mkdir -p /var/www/alumunion/.staging
   sudo chown deploy:deploy /var/www/alumunion/.staging
   sudo -u deploy mkdir -p /home/deploy/.ssh
   # paste the PUBLIC half of a new key pair (ssh-keygen -t ed25519 -f deploy_key) into:
   sudo -u deploy nano /home/deploy/.ssh/authorized_keys
   echo 'deploy ALL=(root) NOPASSWD: /usr/bin/bash /opt/alumunion-src/deploy/activate.sh *' | sudo tee /etc/sudoers.d/alumni-deploy
   sudo chmod 440 /etc/sudoers.d/alumni-deploy
   ```

2. **Put this version of the repo on the server once**, so `activate.sh` exists there (it pulls itself on every run after that):

   ```bash
   sudo git -C /opt/alumunion-src pull --ff-only
   ```

3. **GitHub repository settings** (Settings, Secrets and variables, Actions):

   | Kind | Name | Value |
   | --- | --- | --- |
   | Secret | `DEPLOY_HOST` | the server's address |
   | Secret | `DEPLOY_USER` | `deploy` |
   | Secret | `DEPLOY_SSH_KEY` | the **private** half of the key pair (the whole file) |
   | Secret | `DEPLOY_KNOWN_HOSTS` | the output of `ssh-keyscan -H <server address>`, run from your own machine |
   | Variable | `NEXT_PUBLIC_BASE_DOMAIN` | same value as in the server's frontend env files |
   | Variable | `NEXT_PUBLIC_GOOGLE_CLIENT_ID` | same value as in the server's frontend env files |
   | Variable | `NEXT_PUBLIC_GOOGLE_AUTO_REDIRECT` | same value, or empty |

   The three variables are baked into the browser bundle when the frontends are built (see the
   `frontend-*.env.production.local.example` files). They are public values, which is why they are variables, not secrets.
   The backend settings stay where they are, in `/etc/alumunion/*.env` on the server.

4. Optional: under Settings, Environments, **production**, add yourself as a required reviewer if you want to approve
   each deploy before it goes out.

The first run builds everything because the server has no recorded revision for it yet; later runs build only what a
change affects. Watch a run in the Actions tab: each job shows how long its build took.

`deploy.sh` (build on the server) keeps working and is the fallback if GitHub is unavailable. Changes to Nginx, the
systemd unit files or the server's `.env` files still need to be applied by hand on the server, as before.

## If a deploy takes long, do not stop it and start it again

A big change (anything in a shared backend project or `frontend/packages`) rebuilds every .NET service and every
frontend, one after another, and on a small server the first such build is slow. Stopping it and starting it again
wastes the work in progress. Run it in the background instead, so it carries on whether or not you are watching:

```bash
sudo bash /opt/alumunion-src/deploy/deploy-detached.sh          # same targets as deploy.sh
sudo bash /opt/alumunion-src/deploy/deploy-detached.sh --log    # watch one that is already running
sudo bash /opt/alumunion-src/deploy/deploy-detached.sh --status
```

Closing the terminal or pressing Ctrl-C only stops watching. Everything is also written to
`/var/log/alumunion-deploy.log`, which shows exactly which step the time went on (each step prints its duration and
the memory and swap in use).

If a deploy is stopped anyway, running it again carries on: a .NET service already built for the same commit is reused,
and the frontend builds come from the Turborepo cache.

A service that is slow to start no longer makes the next deploy rebuild everything. The deployed revision is recorded as
soon as the new version is installed and started, before the health checks, and each health check now waits up to three
minutes (cold starts and EF migrations can be slow on a small server).

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
