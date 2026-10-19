import { saveProgress } from '../api';

/** The most often that a position is sent to the server (F-RDR-4). */
export const SAVE_INTERVAL_MS = 30_000;

/**
 * Remembers where a person is in a file. A move is sent at once if the last send is more than 30 seconds ago, else after the
 * rest of the 30 seconds; the last move is sent when the reader is closed, the tab is hidden or the page is left.
 *
 * `known` is the position that needs no saving: the one the file already has, or the start of a file that was never read.
 */
export function createProgress(fileId: string, known: string | null, interval = SAVE_INTERVAL_MS) {
  let saved = known;
  let pending: string | null = null;
  let lastSent = 0;
  let timer: ReturnType<typeof setTimeout> | undefined;

  function send(): void {
    clearTimeout(timer);
    timer = undefined;
    if (pending !== null && pending !== saved) {
      saved = pending;
      lastSent = Date.now();
      void saveProgress(fileId, pending);
    }
    pending = null;
  }

  function onHide(): void {
    if (document.visibilityState === 'hidden') {
      send();
    }
  }

  document.addEventListener('visibilitychange', onHide);
  addEventListener('pagehide', send);

  return {
    /** The reader is at this place. */
    move(locator: string): void {
      pending = locator;
      if (locator === saved) {
        clearTimeout(timer);
        timer = undefined;
        return;
      }
      const wait = lastSent + interval - Date.now();
      if (wait <= 0) {
        send();
      } else if (timer === undefined) {
        timer = setTimeout(send, wait);
      }
    },
    /** This place is the start: it needs no saving until the person moves away from it. */
    start(locator: string): void {
      saved = locator;
    },
    /** Sends the last move now and stops listening. */
    close(): void {
      send();
      document.removeEventListener('visibilitychange', onHide);
      removeEventListener('pagehide', send);
    },
  };
}
