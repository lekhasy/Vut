import crypto from 'node:crypto';
import { SESSION_SECRET } from 'astro:env/server';

export interface SessionPayload {
  userId: string;
  email: string;
  displayName: string;
  avatarUrl: string;
  isEmailVerified: boolean;
  iat: number;
}

export const SESSION_COOKIE = 'vut_session';
const SESSION_MAX_AGE = 86400; // 24 hours

function getKey(): Buffer {
  return Buffer.from(SESSION_SECRET, 'base64');
}

export function encrypt(payload: SessionPayload): string {
  const key = getKey();
  const iv = crypto.randomBytes(12);
  const cipher = crypto.createCipheriv('aes-256-gcm', key, iv);

  const plaintext = JSON.stringify(payload);
  const encrypted = Buffer.concat([
    cipher.update(plaintext, 'utf8'),
    cipher.final(),
  ]);
  const authTag = cipher.getAuthTag();

  return [
    iv.toString('base64'),
    authTag.toString('base64'),
    encrypted.toString('base64'),
  ].join('.');
}

export function decrypt(cookie: string): SessionPayload | null {
  try {
    const key = getKey();
    const parts = cookie.split('.');
    if (parts.length !== 3) return null;

    const [ivB64, authTagB64, ciphertextB64] = parts;
    const iv = Buffer.from(ivB64, 'base64');
    const authTag = Buffer.from(authTagB64, 'base64');
    const ciphertext = Buffer.from(ciphertextB64, 'base64');

    const decipher = crypto.createDecipheriv('aes-256-gcm', key, iv);
    decipher.setAuthTag(authTag);
    const decrypted = Buffer.concat([
      decipher.update(ciphertext),
      decipher.final(),
    ]);

    return JSON.parse(decrypted.toString('utf8')) as SessionPayload;
  } catch {
    return null;
  }
}

export function buildSessionCookieHeader(encryptedValue: string): string {
  const parts = [
    `${SESSION_COOKIE}=${encryptedValue}`,
    'HttpOnly',
    'Secure',
    'SameSite=Lax',
    'Path=/',
    `Max-Age=${SESSION_MAX_AGE}`,
  ];
  return parts.join('; ');
}

export function buildClearSessionCookieHeader(): string {
  const parts = [
    `${SESSION_COOKIE}=`,
    'HttpOnly',
    'Secure',
    'SameSite=Lax',
    'Path=/',
    'Max-Age=0',
  ];
  return parts.join('; ');
}
