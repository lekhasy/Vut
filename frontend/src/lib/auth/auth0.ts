function getEnv(key: string): string {
  const value = process.env[key];
  if (!value) throw new Error(`${key} environment variable is not set`);
  return value;
}

export function getAuth0Domain(): string {
  return getEnv('AUTH0_DOMAIN');
}

export function getAuth0ClientId(): string {
  return getEnv('AUTH0_CLIENT_ID');
}

function getAuth0ClientSecret(): string {
  return getEnv('AUTH0_CLIENT_SECRET');
}

function getAuth0Audience(): string {
  return getEnv('AUTH0_AUDIENCE');
}

function getAppUrl(): string {
  return getEnv('APP_URL');
}

export function buildAuthorizationUrl(state: string): string {
  const params = new URLSearchParams({
    response_type: 'code',
    client_id: getAuth0ClientId(),
    redirect_uri: `${getAppUrl()}/auth/callback`,
    scope: 'openid profile email',
    audience: getAuth0Audience(),
    connection: 'github',
    state,
  });
  return `https://${getAuth0Domain()}/authorize?${params}`;
}

export interface Auth0TokenResponse {
  access_token: string;
  id_token: string;
  scope: string;
  expires_in: number;
  token_type: string;
}

export async function exchangeCodeForTokens(
  code: string,
): Promise<Auth0TokenResponse> {
  const response = await fetch(
    `https://${getAuth0Domain()}/oauth/token`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        grant_type: 'authorization_code',
        client_id: getAuth0ClientId(),
        client_secret: getAuth0ClientSecret(),
        code,
        redirect_uri: `${getAppUrl()}/auth/callback`,
      }),
    },
  );

  if (!response.ok) {
    const body = await response.text();
    throw new Error(
      `Auth0 token exchange failed (${response.status}): ${body}`,
    );
  }

  return response.json() as Promise<Auth0TokenResponse>;
}

export function buildLogoutUrl(): string {
  const params = new URLSearchParams({
    returnTo: getAppUrl(),
    client_id: getAuth0ClientId(),
  });
  return `https://${getAuth0Domain()}/v2/logout?${params}`;
}
