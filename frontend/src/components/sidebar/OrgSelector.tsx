import { useState, useEffect } from 'react';
import { organizations, currentOrgId } from '../../stores/organizations';
import { useStore } from '../../hooks/useStore';
import type { Organization } from '../../stores/types';
import { toast } from 'sonner';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '../ui/dropdown-menu';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '../ui/dialog';

export function OrgSelector() {
  const orgs = useStore(organizations) as Organization[];
  const selectedOrgId = useStore(currentOrgId) as string | null;
  const [open, setOpen] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [newOrgName, setNewOrgName] = useState('');
  const [loading, setLoading] = useState(false);

  const selectedOrg = orgs.find((o) => o.orgId === selectedOrgId) || null;
  const displayName = selectedOrg?.name || 'Select Organization';
  const initial = displayName.charAt(0).toUpperCase();

  async function loadOrgs() {
    try {
      const res = await fetch('/api/orgs');
      if (res.ok) {
        const data: Organization[] = await res.json();
        organizations.set(data);
        if (data.length > 0 && !currentOrgId.get()) {
          currentOrgId.set(data[0].orgId);
        }
      }
    } catch (err) {
      console.error('Failed to load orgs:', err);
    }
  }

  useEffect(() => {
    loadOrgs();
  }, []);

  function selectOrg(orgId: string) {
    currentOrgId.set(orgId);
    setOpen(false);
  }

  async function createOrg(e: React.SyntheticEvent) {
    e.preventDefault();
    if (!newOrgName.trim()) return;
    setLoading(true);
    try {
      const res = await fetch('/api/orgs', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: newOrgName.trim() }),
      });
      if (res.ok) {
        const org: Organization = await res.json();
        organizations.set([...organizations.get(), org]);
        currentOrgId.set(org.orgId);
        setCreateOpen(false);
        setNewOrgName('');
        toast.success(`Organization "${org.name}" created`);
      }
    } catch (err) {
      toast.error('Failed to create organization. Please try again.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <>
      <DropdownMenu open={open} onOpenChange={setOpen}>
        <DropdownMenuTrigger asChild>
          <button
            className="flex w-full items-center gap-2 rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm text-[var(--color-text)] hover:bg-[var(--color-border)] transition-colors focus:outline-none focus:ring-2 focus:ring-brand-500"
            aria-haspopup="listbox"
          >
            <span className="inline-flex h-6 w-6 items-center justify-center rounded bg-brand-600 text-xs font-bold text-white">
              {initial}
            </span>
            <span className="flex-1 truncate text-left">{displayName}</span>
            <svg
              xmlns="http://www.w3.org/2000/svg"
              className="h-4 w-4 text-[var(--color-text-secondary)]"
              viewBox="0 0 20 20"
              fill="currentColor"
            >
              <path
                fillRule="evenodd"
                d="M5.23 7.21a.75.75 0 011.06.02L10 11.168l3.71-3.938a.75.75 0 111.08 1.04l-4.25 4.5a.75.75 0 01-1.08 0l-4.25-4.5a.75.75 0 01.02-1.06z"
                clipRule="evenodd"
              />
            </svg>
          </button>
        </DropdownMenuTrigger>

        <DropdownMenuContent align="start" className="w-full min-w-[15rem]">
          <div className="max-h-60 overflow-y-auto py-1">
            {orgs.map((org: Organization) => (
              <DropdownMenuItem
                key={org.orgId}
                onClick={() => selectOrg(org.orgId)}
                className="flex items-center gap-2 cursor-pointer"
              >
                <span className="inline-flex h-6 w-6 items-center justify-center rounded bg-brand-600 text-xs font-bold text-white">
                  {org.name.charAt(0).toUpperCase()}
                </span>
                <span className="flex-1 truncate">{org.name}</span>
                <span className="text-xs text-[var(--color-text-secondary)]">{org.role}</span>
              </DropdownMenuItem>
            ))}
          </div>
          <div className="border-t border-[var(--color-border)] px-2 py-2">
            <button
              onClick={() => {
                setOpen(false);
                setCreateOpen(true);
              }}
              className="flex w-full items-center gap-2 rounded px-2 py-1 text-sm text-brand-500 hover:bg-[var(--color-surface)] cursor-pointer"
            >
              <svg
                xmlns="http://www.w3.org/2000/svg"
                className="h-4 w-4"
                viewBox="0 0 20 20"
                fill="currentColor"
              >
                <path
                  fillRule="evenodd"
                  d="M10 5a1 1 0 011 1v3h3a1 1 0 110 2h-3v3a1 1 0 11-2 0v-3H6a1 1 0 110-2h3V6a1 1 0 011-1z"
                  clipRule="evenodd"
                />
              </svg>
              Create Organization
            </button>
          </div>
        </DropdownMenuContent>
      </DropdownMenu>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Create Organization</DialogTitle>
            <DialogDescription>
              Give your organization a name. You will become the owner.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={createOrg} className="space-y-4">
            <div>
              <label htmlFor="org-name" className="block text-sm font-medium text-[var(--color-text)] mb-1">
                Organization Name
              </label>
              <input
                id="org-name"
                type="text"
                value={newOrgName}
                onChange={(e) => setNewOrgName(e.target.value)}
                required
                className="w-full rounded border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-2 text-sm text-[var(--color-text)] focus:outline-none focus:ring-2 focus:ring-brand-500"
                placeholder="My Organization"
              />
            </div>
            <div className="flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setCreateOpen(false)}
                className="rounded px-4 py-2 text-sm text-[var(--color-text)] hover:bg-[var(--color-surface)]"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={loading}
                className="rounded bg-brand-600 px-4 py-2 text-sm text-white hover:bg-brand-700 disabled:opacity-50"
              >
                {loading ? 'Creating...' : 'Create'}
              </button>
            </div>
          </form>
        </DialogContent>
      </Dialog>
    </>
  );
}
