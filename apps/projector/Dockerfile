# Velucid Projector (Node/Bun) — multi-stage Docker build
# Stage 1: Build with Bun
# Stage 2: Run with Bun (Bun everywhere — see Story 4.0)

FROM oven/bun:1.3.14-alpine AS build
WORKDIR /app
COPY package.json bun.lock* ./
COPY apps/projector/package.json ./apps/projector/
RUN bun install --frozen-lockfile
COPY apps/projector ./apps/projector
WORKDIR /app/apps/projector
RUN bun run build

FROM oven/bun:1.3.14-alpine AS runtime
WORKDIR /app
COPY --from=build /app/apps/projector/dist ./dist
COPY --from=build /app/apps/projector/package.json ./
COPY --from=build /app/apps/projector/node_modules ./node_modules
ENV PORT=3001
EXPOSE 3001
CMD ["bun", "run", "dist/main.js"]
