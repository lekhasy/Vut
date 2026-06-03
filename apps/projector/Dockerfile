# Velucid Projector (Node/Bun) — multi-stage Docker build
# Stage 1: Build with Bun
# Stage 2: Run with Bun (Bun everywhere — see Story 4.0)

FROM oven/bun:1.3.14-alpine AS build
WORKDIR /app
COPY package.json bun.lock* ./
COPY apps/projector/package.json ./apps/projector/
COPY apps/web/package.json ./apps/web/
COPY libs/events/package.json ./libs/events/
COPY libs/kurrent-client/package.json ./libs/kurrent-client/
COPY libs/projection/package.json ./libs/projection/
COPY libs/read-model/package.json ./libs/read-model/
RUN bun install --frozen-lockfile --ignore-scripts
COPY libs ./libs
COPY apps/projector ./apps/projector
WORKDIR /app/apps/projector
RUN bun run build

FROM oven/bun:1.3.14-alpine AS runtime
WORKDIR /app

# Bun workspaces use symlinks: apps/projector/node_modules/X → ../../node_modules/.bun/<hash>/node_modules/X
# We must preserve both the app's node_modules (symlinks) and the root .bun store (targets).
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/apps/projector/node_modules ./apps/projector/node_modules
COPY --from=build /app/apps/projector/dist ./apps/projector/dist
COPY --from=build /app/apps/projector/package.json ./apps/projector/

WORKDIR /app/apps/projector
ENV PORT=3001
EXPOSE 3001
CMD ["bun", "run", "dist/main.js"]
