# syntax=docker/dockerfile:1
# ---------------------------------------------------------------------------
# CloudFilm Portal
# Uses Next.js standalone output, so the runtime image carries a traced subset of
# node_modules instead of the full workspace install.
# ---------------------------------------------------------------------------

FROM node:22-alpine AS base
ENV PNPM_HOME=/pnpm PATH=/pnpm:$PATH
RUN corepack enable

FROM base AS deps
WORKDIR /repo
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY apps/portal/package.json     apps/portal/
COPY packages/shared/package.json packages/shared/
COPY packages/core/package.json   packages/core/
COPY packages/ui/package.json     packages/ui/
# --store-dir is required, not cosmetic. pnpm does not put its store under
# PNPM_HOME: it picks a location on the same filesystem as the project, so
# without this flag the store lands in /repo and the cache mount below caches
# nothing. Every build then re-downloads the entire dependency tree, which is
# both slow and a network failure waiting to happen. Pointing pnpm at the mount
# is what makes the cache actually work.
RUN --mount=type=cache,id=pnpm,target=/pnpm/store \
    pnpm install --frozen-lockfile --store-dir=/pnpm/store

FROM base AS build
WORKDIR /repo
COPY --from=deps /repo/node_modules              node_modules
COPY --from=deps /repo/apps/portal/node_modules  apps/portal/node_modules
COPY --from=deps /repo/packages                  packages
COPY . .

# No application configuration is needed to build. Every address the portal uses
# is read at runtime, so one image is valid for every hospital rather than being
# pinned to the environment it was built for.
ENV NEXT_TELEMETRY_DISABLED=1

RUN pnpm --filter @cloudfilm/portal build

FROM base AS runtime
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
