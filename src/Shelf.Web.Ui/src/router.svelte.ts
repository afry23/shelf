// A small router over the History API: the address is the state of the interface (filters, sort, query), so reload, back and a
// copied link all work. No package: the interface has five routes.

export const router = $state({ path: location.pathname, search: location.search });

function sync(): void {
  router.path = location.pathname;
  router.search = location.search;
}

export function navigate(to: string, options: { replace?: boolean } = {}): void {
  const url = new URL(to, location.origin);
  if (options.replace) {
    history.replaceState(null, '', url);
  } else {
    history.pushState(null, '', url);
  }
  sync();
  window.scrollTo(0, 0);
}

/** Changes the query string of the current page and keeps its path. A value of null or '' removes the parameter. */
export function setParams(changes: Record<string, string | null>, options: { replace?: boolean } = {}): void {
  const params = new URLSearchParams(router.search);
  for (const [key, value] of Object.entries(changes)) {
    if (value === null || value === '') {
      params.delete(key);
    } else {
      params.set(key, value);
    }
  }
  const search = params.toString();
  const url = router.path + (search ? `?${search}` : '');
  if (options.replace) {
    history.replaceState(null, '', url);
  } else {
    history.pushState(null, '', url);
  }
  sync();
}

window.addEventListener('popstate', sync);

/** Lets a click on an address of this page change the route without loading the page again. */
export function handleLinkClick(event: MouseEvent): void {
  if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
    return;
  }
  const link = (event.target as Element | null)?.closest('a');
  if (!link || link.target || link.hasAttribute('download') || link.origin !== location.origin) {
    return;
  }
  // The API and its downloads are files, not routes.
  if (link.pathname.startsWith('/api/')) {
    return;
  }
  event.preventDefault();
  navigate(link.pathname + link.search + link.hash);
}

export function param(name: string, fallback = ''): string {
  return new URLSearchParams(router.search).get(name) ?? fallback;
}
