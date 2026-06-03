import { atom } from 'nanostores';
import type { Organization } from './types';

export const organizations = atom<Organization[]>([]);
export const currentOrgId = atom<string | null>(null);

// Helper to get current org
export function getCurrentOrg(): Organization | null {
  const list = organizations.get();
  const currentId = currentOrgId.get();
  return list.find((o) => o.orgId === currentId) || null;
}

// Helper to set current org by ID
export function setCurrentOrg(orgId: string) {
  currentOrgId.set(orgId);
}

// Helper to add a new org to the list
export function addOrg(org: Organization) {
  organizations.set([...organizations.get(), org]);
}

// Helper to update org in the list
export function updateOrg(orgId: string, updates: Partial<Organization>) {
  const list = organizations.get();
  organizations.set(list.map((o) => (o.orgId === orgId ? { ...o, ...updates } : o)));
}

// Helper to remove org from the list
export function removeOrg(orgId: string) {
  organizations.set(organizations.get().filter((o) => o.orgId !== orgId));
}
