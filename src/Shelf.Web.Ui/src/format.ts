export function plural(count: number, one: string, many = `${one}s`): string {
  return `${count} ${count === 1 ? one : many}`;
}

export function bytes(size: number): string {
  if (size < 1024) {
    return `${size} B`;
  }
  const units = ['KB', 'MB', 'GB'];
  let value = size / 1024;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }
  return `${value.toFixed(value < 10 ? 1 : 0)} ${units[unit]}`;
}

/**
 * The address of the reader for a file, at a place in it. A PDF location is `page=12`, an EPUB location an `epubcfi(...)`
 * (F-RDR-3): `/read/{fileId}#page=12` and `/read/{fileId}#cfi=epubcfi(...)`.
 */
export function readLink(fileId: string, location?: string | null): string {
  const base = `/read/${encodeURIComponent(fileId)}`;
  if (!location) {
    return base;
  }
  return location.startsWith('epubcfi(') ? `${base}#cfi=${location}` : `${base}#${location}`;
}

/** Where a person stopped in a file, for the book page: `page=12` is page 12; an EPUB position has no number to show. */
export function resumeText(locator: string): string {
  const page = /^page=(\d+)$/.exec(locator);
  return page ? `Page ${page[1]}` : 'Where you stopped';
}

/** Splits a snippet of the search into plain and marked parts; the server marks a hit as [word]. */
export function snippetParts(snippet: string): { text: string; hit: boolean }[] {
  const parts: { text: string; hit: boolean }[] = [];
  const pattern = /\[([^\]]+)\]/g;
  let last = 0;
  for (const match of snippet.matchAll(pattern)) {
    if (match.index > last) {
      parts.push({ text: snippet.slice(last, match.index), hit: false });
    }
    parts.push({ text: match[1], hit: true });
    last = match.index + match[0].length;
  }
  if (last < snippet.length) {
    parts.push({ text: snippet.slice(last), hit: false });
  }
  return parts;
}

/** One of six cloth colors for the spine of a book without a cover, always the same for the same work. */
export function clothOf(id: string): number {
  let sum = 0;
  for (const c of id) {
    sum = (sum * 31 + c.charCodeAt(0)) >>> 0;
  }
  return sum % 6;
}

export const authorLine = (authors: string[]) => authors.join(', ');
