// The JSON API of `shelf serve` (/api/v1). Every answer that is not a success is a problem document.

export interface Problem {
  status: number;
  title: string;
  detail?: string;
}

/** An error the server explained, or a request that did not get through. */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
  }
}

export interface Status {
  version: string;
  works: number;
  files: number;
}

export interface WorkItem {
  id: string;
  title: string;
  subtitle: string | null;
  authors: string[];
  year: number | null;
  language: string | null;
  formats: string[];
  tags: string[];
  readStatus: ReadStatus;
  hasCover: boolean;
}

export type ReadStatus = 'unread' | 'reading' | 'read';

export interface FacetValue {
  value: string;
  count: number;
}

export interface Facets {
  tags: FacetValue[];
  authors: FacetValue[];
  languages: FacetValue[];
  years: FacetValue[];
  formats: FacetValue[];
  statuses: FacetValue[];
}

export interface WorkList {
  total: number;
  offset: number;
  limit: number;
  items: WorkItem[];
  facets: Facets;
}

export interface WorkFile {
  id: string;
  name: string;
  format: string;
  sizeBytes: number;
  pages: number | null;
}

export interface TocItem {
  title: string;
  level: number;
  location: string | null;
}

export interface WorkDetail {
  id: string;
  title: string;
  subtitle: string | null;
  authors: string[];
  language: string | null;
  year: number | null;
  isbn13: string | null;
  publisher: string | null;
  series: string | null;
  seriesIndex: number | null;
  edition: string | null;
  description: string | null;
  rating: number | null;
  tags: string[];
  readStatus: ReadStatus;
  hasCover: boolean;
  files: WorkFile[];
  toc: TocItem[];
}

export type SearchMode = 'fulltext' | 'semantic' | 'hybrid';
export type SearchLevel = 'work' | 'chunk';

export interface Passage {
  fileId: string;
  file: string;
  format: string;
  location: string;
  where: string;
  snippet: string;
}

export interface SearchItem {
  id: string;
  title: string;
  authors: string[];
  year: number | null;
  language: string | null;
  formats: string[];
  score: number;
  passages: Passage[];
}

export interface SearchResponse {
  query: string;
  mode: SearchMode;
  level: SearchLevel;
  total: number;
  pendingFiles: number;
  worksWithoutEmbedding: number;
  note: string | null;
  results: SearchItem[];
}

export interface DuplicateFile {
  id: string;
  workId: string;
  title: string;
  name: string;
  path: string;
  sizeBytes: number;
  pages: number | null;
  completeness: number | null;
}

export interface DuplicateGroup {
  kind: 'identical' | 'same-format';
  format: string;
  keep: number;
  files: DuplicateFile[];
}

export interface DuplicateList {
  total: number;
  groups: DuplicateGroup[];
}

export interface DuplicateOutcome {
  trashed: number;
  worksRemoved: string[];
}

export interface TagNode {
  name: string;
  path: string;
  works: number;
  direct: number;
  children: TagNode[];
}

export interface TagTree {
  tags: number;
  roots: TagNode[];
}

async function request<T>(method: string, path: string, body?: unknown, contentType = 'application/json'): Promise<T> {
  const response = await fetch(`/api/v1${path}`, {
    method,
    headers: { Accept: 'application/json', ...(body === undefined ? {} : { 'Content-Type': contentType }) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (response.status === 401) {
    throw new ApiError(
      'This server needs an access token. Open the address that shelf serve was started for with ?token=… at the end, once; the browser keeps the token after that.',
      401,
    );
  }
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as Problem | null;
    throw new ApiError(problem?.detail ?? problem?.title ?? `HTTP ${response.status}`, response.status);
  }
  return (await response.json()) as T;
}

const query = (params: Record<string, string | number | undefined>): string => {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') {
      search.set(key, String(value));
    }
  }
  const text = search.toString();
  return text ? `?${text}` : '';
};

export const status = () => request<Status>('GET', '/status');

export const works = (params: { q?: string; sort?: string; order?: string; offset?: number; limit?: number }) =>
  request<WorkList>('GET', `/works${query(params)}`);

export const work = (id: string) => request<WorkDetail>('GET', `/works/${encodeURIComponent(id)}`);

export const editWork = (id: string, patch: Record<string, unknown>) =>
  request<WorkDetail>('PATCH', `/works/${encodeURIComponent(id)}`, patch, 'application/merge-patch+json');

export const setTags = (id: string, tags: string[]) =>
  request<{ id: string; tags: string[]; added: number; removed: number }>('PUT', `/works/${encodeURIComponent(id)}/tags`, { tags });

export const tags = () => request<TagTree>('GET', '/tags');

export const search = (params: { q: string; mode?: SearchMode; level?: SearchLevel; limit?: number }) =>
  request<SearchResponse>('GET', `/search${query(params)}`);

export const duplicates = () => request<DuplicateList>('GET', '/duplicates');

export const duplicateAction = (action: 'keep' | 'trash' | 'ignore', fileId: string) =>
  request<DuplicateOutcome>('POST', `/duplicates/${action}`, { fileId });

export const coverUrl = (id: string) => `/api/v1/works/${encodeURIComponent(id)}/cover`;

export const fileUrl = (id: string, download = false) => `/api/v1/files/${encodeURIComponent(id)}${download ? '?download=true' : ''}`;
