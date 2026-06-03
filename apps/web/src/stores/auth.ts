import { atom } from 'nanostores';
import type { User } from './types';

export const currentUser = atom<User | null>(null);
export const isAuthenticated = atom<boolean>(false);
