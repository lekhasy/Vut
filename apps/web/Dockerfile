# Velucid Web (Astro SSR) — multi-stage Docker build
# Stage 1: Build the Astro SSR app with Bun
# Stage 2: Run with Bun on port 3000 (Bun everywhere — see Story 4.0)

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
COPY --from=build /app/apps/web/dist ./dist
COPY --from=build /app/apps/web/package.json ./
COPY --from=build /app/apps/web/node_modules ./node_modules
ENV HOST=0.0.0.0
ENV PORT=3000
EXPOSE 3000
CMD ["bun", "./dist/server/entry.mjs"]
