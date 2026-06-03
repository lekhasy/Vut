import { describe, it, expect, beforeAll, afterAll } from 'bun:test';
import Fastify from 'fastify';

describe('projector skeleton', () => {
  let app: ReturnType<typeof Fastify>;

  beforeAll(async () => {
    app = Fastify({ logger: false });
    app.get('/health', async () => ({ status: 'ok', service: 'projector', version: '0.0.0' }));
    await app.listen({ port: 0, host: '127.0.0.1' });
  });

  afterAll(async () => {
    await app.close();
  });

  it('GET /health returns ok', async () => {
    const address = app.server.address();
    if (!address || typeof address === 'string') throw new Error('expected tcp address');
    const res = await fetch(`http://127.0.0.1:${address.port}/health`);
    expect(res.status).toBe(200);
    const body = (await res.json()) as { status: string; service: string };
    expect(body.status).toBe('ok');
    expect(body.service).toBe('projector');
  });
});
