import { quotePassage } from '../api';

/** Puts text on the clipboard. The Clipboard API needs a secure context (localhost or HTTPS); over plain HTTP on a LAN address a hidden field serves. */
async function copyText(text: string): Promise<void> {
  if (navigator.clipboard && window.isSecureContext) {
    await navigator.clipboard.writeText(text);
    return;
  }
  const field = document.createElement('textarea');
  field.value = text;
  field.setAttribute('readonly', '');
  field.style.position = 'fixed';
  field.style.opacity = '0';
  document.body.append(field);
  const selection = getSelection();
  const saved = selection && selection.rangeCount > 0 ? selection.getRangeAt(0) : null;
  field.select();
  const done = document.execCommand('copy');
  field.remove();
  if (saved && selection) {
    selection.removeAllRanges();
    selection.addRange(saved);
  }
  if (!done) {
    throw new Error('The browser did not allow copying. Select the quote and copy it by hand.');
  }
}

/**
 * Copies a passage of one file as a Markdown quote with its citation (F-ANN-8). The text comes from the server, so that the web
 * interface, the CLI and MCP quote in the same way. `message` says what happened and goes away after a few seconds.
 */
export function createQuoteCopy(fileIdOf: () => string) {
  let message = $state<{ text: string; failed: boolean } | null>(null);
  let timer: ReturnType<typeof setTimeout> | undefined;

  function say(text: string, failed: boolean): void {
    message = { text, failed };
    clearTimeout(timer);
    timer = setTimeout(() => (message = null), 5000);
  }

  return {
    get message() {
      return message;
    },
    async copy(passage: string, locator: string): Promise<void> {
      try {
        const citation = await quotePassage(fileIdOf(), passage, locator);
        await copyText(citation.text);
        say('Quote copied', false);
      } catch (e) {
        say((e as Error).message, true);
      }
    },
  };
}
