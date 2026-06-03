import {
  AUTH0_DOMAIN,
  AUTH0_CLIENT_ID,
  AUTH0_CLIENT_SECRET,
  AUTH0_AUDIENCE,
  APP_URL,
} from 'astro:env/server';

export type AuthConnection = 'github';

export function buildAuthorizationUrl(state: string, connection: AuthConnection): string {
  const params = new URLSearchParams({
    response_type: 'code',
    client_id: AUTH0_CLIENT_ID,
    redirect_uri: `${APP_URL}/auth/callback`,
    scope: 'openid profile email',
    audience: AUTH0_AUDIENCE,
    connection,
    state,
  });
  return `https://${AUTH0_DOMAIN}/authorize?${params}`;
}

export interface Auth0TokenResponse {
  access_token: string;
  id_token: string;
  scope: string;
  expires_in: number;
  token_type: string;
}

export async function exchangeCodeForTokens(code: string): Promise<Auth0TokenResponse> {
  const response = await fetch(`https://${AUTH0_DOMAIN}/oauth/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      grant_type: 'authorization_code',
      client_id: AUTH0_CLIENT_ID,
      client_secret: AUTH0_CLIENT_SECRET,
      code,
      redirect_uri: `${APP_URL}/auth/callback`,
    }),
  });

  if (!response.ok) {
    const body = await response.text();
    throw new Error(`Auth0 token exchange failed (${response.status}): ${body}`);
  }

  return response.json() as Promise<Auth0TokenResponse>;
}

export function buildLogoutUrl(): string {
  const params = new URLSearchParams({
    returnTo: APP_URL,
    client_id: AUTH0_CLIENT_ID,
  });
  return `https://${AUTH0_DOMAIN}/v2/logout?${params}`;
}
