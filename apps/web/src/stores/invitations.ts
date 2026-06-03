import { atom } from 'nanostores';
import type { Invitation } from './types';

export const pendingInvitations = atom<Invitation[]>([]);
