import type { APIRoute } from 'astro';
import { buildLogoutUrl } from '@/lib/auth/auth0';
import { buildClearSessionCookieHeader } from '@/lib/auth/session';

export const GET: APIRoute = async () => {
  const logoutUrl = buildLogoutUrl();

  return new Response(null, {
    status: 302,
    headers: {
      Location: logoutUrl,
      'Set-Cookie': buildClearSessionCookieHeader(),
    },
  });
};
