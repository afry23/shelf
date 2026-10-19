// Keyboard control (F-WEB-5): `/` goes to the search field, `j` and `k` move through the items of the page, `Enter` opens the one
// that is current. The items are the elements marked `data-nav` (a book, a hit, a duplicate, a review entry); the current one has
// the focus, so that a screen reader says where the person is and the browser scrolls to it.

const typing = (target: EventTarget | null): boolean => {
  const element = target as HTMLElement | null;
  return !!element && (element.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(element.tagName));
};

const items = (): HTMLElement[] => [...document.querySelectorAll<HTMLElement>('main [data-nav]')];

function move(step: 1 | -1): void {
  const all = items();
  if (all.length === 0) {
    return;
  }
  const here = all.findIndex((item) => item === document.activeElement || item.contains(document.activeElement));
  const next = here < 0 ? (step === 1 ? 0 : all.length - 1) : Math.min(Math.max(here + step, 0), all.length - 1);
  const item = all[next];
  item.tabIndex = -1;
  item.focus();
  item.scrollIntoView({ block: 'nearest' });
}

/** Handles a key press of the whole page. Returns nothing; a key that is meant for the page is not passed on. */
export function handleKey(event: KeyboardEvent): void {
  if (event.defaultPrevented || event.ctrlKey || event.metaKey || event.altKey) {
    return;
  }
  if (event.key === 'Escape' && typing(event.target)) {
    (event.target as HTMLElement).blur();
    return;
  }
  if (typing(event.target)) {
    return;
  }
  switch (event.key) {
    case '/': {
      const field = document.querySelector<HTMLInputElement>('input[type="search"][aria-label="Search your library"]');
      if (field) {
        event.preventDefault();
        field.focus();
        field.select();
      }
      break;
    }
    case 'j':
    case 'k':
      event.preventDefault();
      move(event.key === 'j' ? 1 : -1);
      break;
    case 'Enter': {
      // Only on an item itself: a link or a button that has the focus does what Enter always does.
      const active = document.activeElement as HTMLElement | null;
      if (active?.hasAttribute('data-nav')) {
        const link = active.querySelector<HTMLAnchorElement>('a[href]');
        if (link) {
          event.preventDefault();
          link.click();
        }
      }
      break;
    }
  }
}
