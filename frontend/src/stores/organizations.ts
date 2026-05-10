import { atom } from 'nanostores';
import type { Organization } from './types';

export const organizations = atom<Organization[]>([]);
export const currentOrgId = atom<string | null>(null);
