# syntax=docker/dockerfile:1
# ---------------------------------------------------------------------------
# CloudFilm Portal
# Uses Next.js standalone output, so the runtime image carries a traced subset of
# node_modules instead of the full workspace install.
# ---------------------------------------------------------------------------

FROM node:26-alpine AS build
ENV CI=true \
    NEXT_TELEMETRY_DISABLED=1 \
    COREPACK_ENABLE_DOWNLOAD_PROMPT=0 \
    npm_config_fund=false \
    npm_config_audit=false \
    npm_config_update_notifier=false

RUN npm install --global --no-fund --no-audit pnpm@11.17.0

WORKDIR /repo

# Dependency manifests only, ahead of the source tree.
#
# This layer is keyed on the lockfile and the workspace manifests, so editing a
# component does not invalidate it and the install below is skipped entirely.
# Copying the whole tree first made every source edit re-run a full install.
#
# These are exactly the five importers pnpm-lock.yaml declares. --frozen-lockfile
# fails if any is missing, and COPY fails on a path that does not exist, so this
# list and the workspace must stay in step. apps/api and apps/retrieve are .NET
# and have no package.json.
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY apps/portal/package.json     apps/portal/
COPY packages/core/package.json   packages/core/
COPY packages/shared/package.json packages/shared/
COPY packages/ui/package.json     packages/ui/

# --store-dir is required, not decoration.
#
# pnpm does not keep its store at ~/.local/share/pnpm/store unconditionally: it
# relocates the store onto the same filesystem as the project so it can hardlink
# into node_modules. A cache mounted at the default path is a different device
# from /repo, which is precisely the condition that makes pnpm move the store
# somewhere else - and the mount would then cache nothing. Naming the directory
# explicitly makes the store location independent of filesystem topology.
#
# The store outlives the layer, so a lockfile change re-links packages but does
# not re-download them.
RUN --mount=type=cache,id=cloudfilm-pnpm-store,target=/pnpm-store \
    pnpm install --frozen-lockfile --store-dir=/pnpm-store

# Source tree. Everything above this line is cached across source edits.
COPY . .

RUN pnpm --filter @cloudfilm/portal build

FROM node:26-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0

COPY --from=build /repo/apps/portal/.next/standalone ./
COPY --from=build /repo/apps/portal/.next/static      ./apps/portal/.next/static
COPY --from=build /repo/apps/portal/public            ./apps/portal/public

USER node
EXPOSE 3000

CMD ["node", "apps/portal/server.js"]
