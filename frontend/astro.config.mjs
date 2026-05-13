import { defineConfig, envField } from 'astro/config';
import tailwind from '@astrojs/tailwind';
import node from '@astrojs/node';

export default defineConfig({
  integrations: [tailwind()],
  output: 'server',
  adapter: node({
    mode: 'standalone',
  }),
  env: {
    schema: {
      AUTH0_DOMAIN: envField.string({ context: 'server', access: 'secret' }),
      AUTH0_CLIENT_ID: envField.string({ context: 'server', access: 'secret' }),
      AUTH0_CLIENT_SECRET: envField.string({ context: 'server', access: 'secret' }),
      AUTH0_AUDIENCE: envField.string({ context: 'server', access: 'secret' }),
      SESSION_SECRET: envField.string({ context: 'server', access: 'secret' }),
      SILO_API_URL: envField.string({ context: 'server', access: 'secret' }),
      APP_URL: envField.string({ context: 'server', access: 'secret' }),
      PUBLIC_API_BASE_URL: envField.string({ context: 'client', access: 'public', optional: true, default: '' }),
    },
  },
});
