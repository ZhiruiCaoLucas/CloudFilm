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
RUN --mount=type=cache,id=pnpm,target=/pnpm/store \
    pnpm install --frozen-lockfile

FROM base AS build
WORKDIR /repo
COPY --from=deps /repo/node_modules              node_modules
COPY --from=deps /repo/apps/portal/node_modules  apps/portal/node_modules
COPY --from=deps /repo/packages                  packages
COPY . .

# Only the public viewer address is needed at build time; it is inlined into the
# client bundle. The API address is read at runtime and never reaches the browser.
ARG NEXT_PUBLIC_VIEWER_URL
ENV NEXT_PUBLIC_VIEWER_URL=$NEXT_PUBLIC_VIEWER_URL
ENV CLOUDFILM_API_URL=http://api:8080
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
