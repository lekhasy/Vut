import { defineMiddleware } from 'astro:middleware';
import { decrypt, SESSION_COOKIE } from '@/lib/auth/session';

const PUBLIC_PATHS = new Set(['/', '/auth/login', '/auth/callback']);

function isPublicPath(pathname: string): boolean {
  if (PUBLIC_PATHS.has(pathname)) return true;

  // Static assets
  if (
    pathname.startsWith('/_astro/') ||
    pathname.startsWith('/favicon') ||
    pathname.match(/\.\w+$/) // files with extensions (css, js, images, etc.)
  ) {
    return true;
  }

  return false;
}

function isAuthPath(pathname: string): boolean {
  return pathname.startsWith('/auth/');
}

export const onRequest = defineMiddleware(async (context, next) => {
  const { pathname } = context.url;

  // Skip auth for public paths
  if (isPublicPath(pathname)) {
    return next();
  }

  // Allow auth routes to proceed (login, callback, logout)
  if (isAuthPath(pathname)) {
    return next();
  }

  // Extract and decrypt session cookie
  const sessionCookie = context.cookies.get(SESSION_COOKIE)?.value;

  if (!sessionCookie) {
    return context.redirect('/auth/login', 302);
  }

  const session = decrypt(sessionCookie);

  if (!session) {
    // Invalid or tampered cookie
    context.cookies.delete(SESSION_COOKIE, { path: '/' });
    return context.redirect('/auth/login', 302);
  }

  // Attach session data to Astro.locals
  context.locals.userId = session.userId;
  context.locals.displayName = session.displayName;
  context.locals.avatarUrl = session.avatarUrl;
  context.locals.isEmailVerified = session.isEmailVerified;

  return next();
});
