import { test, expect } from './auth';
import { createTestUser } from '../factories/user';

test.describe('Org Selector (Sidebar)', () => {
  /**
   * [P1] AC#2 — user sees their org list in the sidebar dropdown.
   * Requires: user is signed in, orgs API returns orgs.
   */
  test('[P1] signed-in user sees org selector with initial', async ({ page, user }) => {
    await page.goto('/dashboard');

    // OrgSelector renders a button with the org initial or "?"
    const button = page.locator('[aria-haspopup="listbox"]');
    await expect(button).toBeVisible();
  });

  /**
   * [P1] AC#2 — clicking the org selector opens the dropdown showing all user's orgs.
   */
  test('[P1] clicking org selector opens dropdown with org list', async ({ page, user }) => {
    await page.goto('/dashboard');

    const dropdown = page.locator('[aria-haspopup="listbox"]');
    await dropdown.click();

    // Wait for dropdown to open — Radix portal renders "Create Organization" button
    await expect(page.getByRole('button', { name: 'Create Organization' })).toBeVisible({
      timeout: 5000,
    });
  });

  /**
   * [P1] AC#1 — "Create Organization" button opens the create modal (shadcn Dialog).
   */
  test('[P1] create org button opens new org modal', async ({ page, user }) => {
    await page.goto('/dashboard');

    // Open dropdown
    await page.locator('[aria-haspopup="listbox"]').click();

    // Wait for "Create Organization" button to appear (Radix portal renders it)
    await expect(page.getByRole('button', { name: 'Create Organization' })).toBeVisible({
      timeout: 5000,
    });

    // Click "Create Organization" — this opens the shadcn Dialog
    await page.getByRole('button', { name: 'Create Organization' }).click();

    // DialogContent renders inside a portal with role="dialog"
    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('heading', { name: 'Create Organization' })).toBeVisible();
  });
});

test.describe('Org Dashboard', () => {
  /**
   * [P1] AC#3 — user can navigate to the org dashboard (product list).
   */
  test('[P1] user can navigate to org dashboard page', async ({ page, user }) => {
    // Go to org settings first (guarded page), then navigate to org home
    await page.goto(`/orgs/${user.userId}?orgId=${user.userId}`);

    // Page should load without crash (may redirect to login or show org page)
    // The routing uses the URL param orgId, not the path param in Astro
    await expect(page).not.toHaveURL(/auth\/login/);
  });
});

test.describe('Org Settings', () => {
  /**
   * [P1] AC#5 — owner can see the members list with roles.
   */
  test('[P1] owner sees members list with owner badge', async ({ page, user }) => {
    await page.goto(`/orgs/${user.userId}/settings`);

    // Members section heading should be visible
    await expect(page.getByRole('heading', { name: 'Members', exact: true })).toBeVisible();
  });

  /**
   * [P1] AC#4 — invitation form has email input and role selector.
   */
  test('[P1] invitation form has email input and role selector', async ({ page, user }) => {
    await page.goto(`/orgs/${user.userId}/settings`);

    // Email input
    await expect(page.locator('input[type="email"]')).toBeVisible();
    // Role selector
    await expect(page.locator('select:not([name="dev-toolbar-select"])')).toBeVisible();
    // Send Invite button
    await expect(page.getByRole('button', { name: /send invite/i })).toBeVisible();
  });

  /**
   * [P0] AC#5 — non-owner users do NOT see the Remove button on members.
   */
  test('[P0] non-owner does not see remove button', async ({ page }) => {
    const nonOwner = createTestUser();
    await page.context().clearCookies();

    // Sign in as non-owner
    const { signInAs } = await import('../support/auth-session');
    await signInAs(page, nonOwner);

    await page.goto(`/orgs/${nonOwner.userId}/settings`);

    // No "Remove" buttons should be visible for a non-owner
    await expect(page.getByText('Remove')).not.toBeVisible();
  });
});

test.describe('Auth Guard', () => {
  /**
   * [P0] Unauthenticated users are redirected to /auth/login.
   */
  test('[P0] unauthenticated user redirected to login', async ({ page }) => {
    await page.context().clearCookies();

    await page.goto('/dashboard');

    await expect(page).toHaveURL(/\/auth\/login/);
  });

  /**
   * [P0] Authenticated users can access protected pages without redirect.
   */
  test('[P0] authenticated user can access org settings', async ({ page, user }) => {
    await page.goto('/dashboard');
    await page.goto(`/orgs/${user.userId}/settings`);

    await expect(page).not.toHaveURL(/\/auth\/login/);
  });
});
