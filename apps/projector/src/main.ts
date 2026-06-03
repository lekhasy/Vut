// Skeleton — populated in Story 4.2 (Port Projector to Node/TypeScript)
// This file's only job in Story 4.0 is to confirm Bun + Fastify + the
// workspace structure work end-to-end. The real projector subscribes to
// KurrentDB, projects events, and writes Postgres + OpenFGA tuples.

import Fastify from 'fastify';

const PORT = Number(process.env.PORT ?? 3001);
const VERSION = '0.0.0';

const app = Fastify({ logger: true });

app.get('/health', async () => ({
  status: 'ok',
  service: 'projector',
  version: VERSION,
}));

const shutdown = async (signal: string) => {
  app.log.info(`Received ${signal}, shutting down`);
  await app.close();
  process.exit(0);
};

process.on('SIGINT', () => shutdown('SIGINT'));
process.on('SIGTERM', () => shutdown('SIGTERM'));

try {
  await app.listen({ port: PORT, host: '0.0.0.0' });
} catch (err) {
  app.log.error(err);
  process.exit(1);
}
