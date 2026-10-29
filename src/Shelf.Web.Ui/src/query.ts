// The filters of the query language (tag: author: publisher: lang: year: format: status: series: type:) as buttons: a facet adds one token to `q`, a chip
// removes one. The server parses `q`; this file only builds and splits tokens.

import type { Facets } from './api';

export type FacetKey = keyof Facets;

export const facetGroups: { key: FacetKey; label: string; field: string }[] = [
  { key: 'tags', label: 'Tags', field: 'tag' },
  { key: 'authors', label: 'Authors', field: 'author' },
  { key: 'publishers', label: 'Publisher', field: 'publisher' },
  { key: 'languages', label: 'Language', field: 'lang' },
  { key: 'years', label: 'Year', field: 'year' },
  { key: 'formats', label: 'Format', field: 'format' },
  { key: 'statuses', label: 'Reading status', field: 'status' },
  { key: 'series', label: 'Series', field: 'series' },
  { key: 'types', label: 'Type', field: 'type' },
];

/** The words of a query, with a quoted phrase kept together (a quote inside a token such as tag:"a b" does not split it). */
export function tokens(q: string): string[] {
  const result: string[] = [];
  let current = '';
  let quoted = false;
  for (const c of q) {
    if (c === '"') {
      quoted = !quoted;
      current += c;
    } else if (/\s/.test(c) && !quoted) {
      if (current) {
        result.push(current);
        current = '';
      }
    } else {
      current += c;
    }
  }
  if (current) {
    result.push(current);
  }
  return result;
}

const needsQuotes = (value: string) => /[\s"]/.test(value) || value.includes('/');

export function filterToken(field: string, value: string): string {
  return `${field}:${needsQuotes(value) ? `"${value.replaceAll('"', '')}"` : value}`;
}

export const hasToken = (q: string, token: string) => tokens(q).includes(token);

export const addToken = (q: string, token: string) => (hasToken(q, token) ? q : [...tokens(q), token].join(' '));

export const removeToken = (q: string, token: string) => tokens(q).filter((t) => t !== token).join(' ');

/** "Format: pdf" for format:pdf; the token itself when it is not one of the filters of the facets. */
export function describeToken(token: string): string {
  const negated = token.startsWith('-');
  const match = /^(-?)([a-z]+):(.*)$/.exec(token);
  if (!match) {
    return token;
  }
  const group = facetGroups.find((g) => g.field === match[2]);
  if (!group) {
    return token;
  }
  const value = match[3].replace(/^"(.*)"$/, '$1');
  return `${negated ? 'Not ' : ''}${group.label}: ${value}`;
}
