/** Navigate to a new path using the History API. */
export function navigate(path: string): void {
  window.history.pushState({}, '', path);
  window.dispatchEvent(new PopStateEvent('popstate'));
}

/** Get the current pathname. */
export function currentPath(): string {
  return window.location.pathname;
}
