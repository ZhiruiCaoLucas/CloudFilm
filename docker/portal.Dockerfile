# syntax=docker/dockerfile:1
# ---------------------------------------------------------------------------
# CloudFilm Portal
# Uses Next.js standalone output, so the runtime image carries a traced subset of
# node_modules instead of the full workspace install.
# ---------------------------------------------------------------------------

FROM node:22-alpine AS build
ENV CI=true \
    NEXT_TELEMETRY_DISABLED=1 \
    COREPACK_ENABLE_DOWNLOAD_PROMPT=0 \
    npm_config_fund=false \
    npm_config_audit=false \
    npm_config_update_notifier=false

RUN npm install --global --no-fund --no-audit pnpm@11.17.0

WORKDIR /repo
COPY . .

RUN pnpm install --frozen-lockfile
RUN pnpm --filter @cloudfilm/portal build

FROM node:22-alpine AS runtime
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
