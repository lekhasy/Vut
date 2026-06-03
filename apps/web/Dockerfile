# Velucid Web (Astro SSR) — multi-stage Docker build
# Build:  install all deps, run astro check + astro build, produce dist/
# Runtime: production-only install (no devDeps) + the compiled dist/

FROM oven/bun:1.3.14-alpine AS build
WORKDIR /app
COPY package.json bun.lock* ./
COPY apps/web/package.json ./apps/web/
COPY apps/projector/package.json ./apps/projector/
COPY libs/events/package.json ./libs/events/
COPY libs/kurrent-client/package.json ./libs/kurrent-client/
COPY libs/projection/package.json ./libs/projection/
COPY libs/read-model/package.json ./libs/read-model/
RUN bun install --frozen-lockfile --ignore-scripts
COPY libs ./libs
COPY apps/web ./apps/web
WORKDIR /app/apps/web
RUN bun run build

FROM oven/bun:1.3.14-alpine AS runtime
WORKDIR /app

# Bun workspaces use symlinks: apps/web/node_modules/react-dom → ../../node_modules/.bun/<hash>/node_modules/react-dom
# We must preserve both the app's node_modules (symlinks) and the root .bun store (targets).
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/apps/web/node_modules ./apps/web/node_modules
COPY --from=build /app/apps/web/dist ./apps/web/dist
COPY --from=build /app/apps/web/package.json ./apps/web/

WORKDIR /app/apps/web
ENV HOST=0.0.0.0
ENV PORT=3000
EXPOSE 3000
CMD ["bun", "./dist/server/entry.mjs"]
