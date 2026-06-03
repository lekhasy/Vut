import { createRemoteJWKSet, jwtVerify } from 'jose';
import { AUTH0_DOMAIN, AUTH0_CLIENT_ID } from 'astro:env/server';

export interface IdTokenClaims {
  sub: string;
  nickname?: string;
  name?: string;
  picture?: string;
  email?: string;
}

let jwks: ReturnType<typeof createRemoteJWKSet> | null = null;

function getJWKS(): ReturnType<typeof createRemoteJWKSet> {
  if (!jwks) {
    const url = new URL(`https://${AUTH0_DOMAIN}/.well-known/jwks.json`);
    jwks = createRemoteJWKSet(url);
  }
  return jwks;
}

export async function validateIdToken(idToken: string): Promise<IdTokenClaims> {
  const { payload } = await jwtVerify(idToken, getJWKS(), {
    issuer: `https://${AUTH0_DOMAIN}/`,
    audience: AUTH0_CLIENT_ID,
  });

  if (!payload.sub) {
    throw new Error('ID token missing sub claim');
  }

  return {
    sub: payload.sub,
    nickname: payload.nickname as string | undefined,
    name: payload.name as string | undefined,
    picture: payload.picture as string | undefined,
    email: payload.email as string | undefined,
  };
}
