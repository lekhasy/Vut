import { currentUser, isAuthenticated } from '../stores/auth';
import { apiFetch } from './api';
import type { User } from '../stores/types';

export async function loadSession(): Promise<boolean> {
  try {
    const user = await apiFetch<User>('/api/users/me');
    currentUser.set(user);
    isAuthenticated.set(true);
    return true;
  } catch {
    currentUser.set(null);
    isAuthenticated.set(false);
    return false;
  }
}

export function login(): void {
  window.location.href = '/auth/login';
}

export function logout(): void {
  currentUser.set(null);
  isAuthenticated.set(false);
  window.location.href = '/auth/login';
}

export function requireAuth(): void {
  if (!isAuthenticated.get()) {
    window.location.href = '/auth/login';
  }
}
