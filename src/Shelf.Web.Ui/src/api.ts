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

/** Where a person stopped reading a file (F-RDR-4): `page=N` for a PDF, an `epubcfi(...)` for an EPUB. */
export interface ReadingPosition {
  locator: string;
  updatedAt: string;
}

export interface WorkFile {
  id: string;
  name: string;
  format: string;
  sizeBytes: number;
  pages: number | null;
  progress: ReadingPosition | null;
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
  citeKey: string | null;
  description: string | null;
  rating: number | null;
  tags: string[];
  readStatus: ReadStatus;
  hasCover: boolean;
  files: WorkFile[];
  /** The ID of the file that "Read" opens (F-RDR-5), or null for a work without files. */
  readFile: string | null;
  toc: TocItem[];
}

export type SearchMode = 'fulltext' | 'semantic' | 'hybrid';
export type SearchLevel = 'work' | 'chunk';
export type SearchScope = 'all' | 'title';

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

export interface SimilarResponse {
  id: string;
  title: string;
  total: number;
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

export interface ProbableWork {
  id: string;
  title: string;
  authors: string[];
  folder: string;
}

/** Works that probably are one book: `hamming` is the distance of the text fingerprints, `cosine` the similarity of the vectors. */
export interface ProbablePair {
  hamming: number | null;
  cosine: number | null;
  a: ProbableWork;
  b: ProbableWork;
}

export interface ProbableList {
  total: number;
  note: string | null;
  pairs: ProbablePair[];
}

export interface DuplicateOutcome {
  trashed: number;
  worksRemoved: string[];
}

export interface SplitOutcome {
  work: string;
  newWork: string;
  file: string;
  folder: string;
}

export interface DeleteOutcome {
  work: string;
  trashed: number;
  files: string[];
  workRemoved: boolean;
}

export interface MergeOutcome {
  id: string;
  merged: string;
  movedFiles: number;
  leftoverFolder?: string;
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

/** Sends a file as the body of the request, as it is: the server reads the name from `?name=`. */
async function upload<T>(path: string, file: File): Promise<T> {
  const response = await fetch(`/api/v1${path}`, {
    method: 'POST',
    headers: { Accept: 'application/json', 'Content-Type': 'application/octet-stream' },
    body: file,
  });
  if (response.status === 401) {
    throw new ApiError('This server needs an access token. Open the address that shelf serve was started for with ?token=… at the end, once.', 401);
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

export const work = async (id: string) => {
  await Promise.allSettled([...saving]);
  return request<WorkDetail>('GET', `/works/${encodeURIComponent(id)}`);
};

// Saves that have not been answered yet. They go one after the other, so that an older position never arrives after a newer one,
// and a page that shows the progress of a file waits for them: leaving the reader and opening the book page must not read the
// file before the position is written.
const saving = new Set<Promise<unknown>>();
let lastSave: Promise<unknown> = Promise.resolve();

/**
 * Saves the reading position of a file. `keepalive` lets the request finish when the page is being closed.
 * A failure is not shown: the next move saves again.
 */
export const saveProgress = (fileId: string, locator: string): Promise<unknown> => {
  // A page that is going away cannot wait for the one before: its request has to leave now (`keepalive` carries it on).
  const before = document.visibilityState === 'hidden' ? Promise.resolve() : lastSave;
  const sent: Promise<unknown> = before
    .then(() =>
      fetch(`/api/v1/files/${encodeURIComponent(fileId)}/progress`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ locator }),
        keepalive: true,
        credentials: 'same-origin',
      }),
    )
    .catch(() => undefined)
    .finally(() => saving.delete(sent));
  saving.add(sent);
  lastSave = sent;
  return sent;
};

export const similar = (id: string, limit = 6) =>
  request<SimilarResponse>('GET', `/works/${encodeURIComponent(id)}/similar?limit=${limit}`);

export const editWork = (id: string, patch: Record<string, unknown>) =>
  request<WorkDetail>('PATCH', `/works/${encodeURIComponent(id)}`, patch, 'application/merge-patch+json');

export const suggestCiteKey = (id: string) =>
  request<{ id: string; citeKey: string | null }>('POST', `/works/${encodeURIComponent(id)}/cite-key/suggest`);

export const setTags = (id: string, tags: string[]) =>
  request<{ id: string; tags: string[]; added: number; removed: number }>('PUT', `/works/${encodeURIComponent(id)}/tags`, { tags });

export const tags = () => request<TagTree>('GET', '/tags');

export interface TagMoveResult {
  from: string;
  to: string;
  changed: number;
}

export const renameTag = (from: string, to: string) => request<TagMoveResult>('POST', '/tags/rename', { from, to });

export const mergeTag = (from: string, into: string) => request<TagMoveResult>('POST', '/tags/merge', { from, into });

export interface ReviewCandidate {
  work: string;
  title: string | null;
  confidence: number;
  signals: string[];
}

export interface ReviewEntry {
  id: string;
  kind: 'grouping' | 'extraction-error' | 'weak-metadata';
  createdAt: string;
  message: string;
  file: string | null;
  work: string | null;
  workTitle: string | null;
  candidates: ReviewCandidate[];
}

export interface ReviewList {
  total: number;
  entries: ReviewEntry[];
}

export interface ReviewOutcome {
  entry: string;
  into: string | null;
  movedFiles: number;
}

export const review = () => request<ReviewList>('GET', '/review');

export const acceptReview = (id: string, into?: string) =>
  request<ReviewOutcome>('POST', `/review/${encodeURIComponent(id)}/accept`, into ? { into } : {});

export const dismissReview = (id: string) => request<ReviewOutcome>('POST', `/review/${encodeURIComponent(id)}/dismiss`, {});

export interface PlannedMove {
  id: string;
  from: string;
  to: string;
}

export interface SkippedMove extends PlannedMove {
  reason: string;
}

export interface OrganizePreview {
  pattern: string;
  files: number;
  works: number;
  unchanged: number;
  truncated: boolean;
  moves: PlannedMove[];
  collisions: SkippedMove[];
  /** The label of the run that "undo" would revert now, or null. */
  nextUndo: string | null;
}

export interface OrganizeOutcome {
  pattern: string;
  files: number;
  works: number;
  unchanged: number;
  collisions: SkippedMove[];
}

export interface UndoOutcome {
  label: string;
  reverted: number;
  skipped: { path: string; reason: string }[];
}

export const previewOrganize = (pattern?: string) => request<OrganizePreview>('GET', `/organize${pattern ? query({ pattern }) : ''}`);

export const applyOrganize = (pattern: string) => request<OrganizeOutcome>('POST', '/organize', { pattern });

export const undoLast = () => request<UndoOutcome>('POST', '/undo', {});

export const search = (params: { q: string; mode?: SearchMode; level?: SearchLevel; scope?: SearchScope; limit?: number }) =>
  request<SearchResponse>('GET', `/search${query(params)}`);

export const duplicates = () => request<DuplicateList>('GET', '/duplicates');

export const probableDuplicates = () => request<ProbableList>('GET', '/duplicates/probable');

export const ignoreProbable = (a: string, b: string) => request<DuplicateOutcome>('POST', '/duplicates/probable/ignore', { a, b });

export const duplicateAction = (action: 'keep' | 'trash' | 'ignore', fileId: string) =>
  request<DuplicateOutcome>('POST', `/duplicates/${action}`, { fileId });

/** Merges the work `source` into `target`; the files of `source` move over and its folder goes. It cannot be undone. */
export const mergeWorks = (target: string, source: string) =>
  request<MergeOutcome>('POST', `/works/${encodeURIComponent(target)}/merge`, { from: source });

/** Moves a file to the trash; the last file of a work takes the work along. `shelf undo` brings it back. */
export const deleteFile = (fileId: string) => request<DeleteOutcome>('DELETE', `/files/${encodeURIComponent(fileId)}`);

/** Moves a work with all its files to the trash. `shelf undo` brings it back. */
export const deleteWork = (workId: string) => request<DeleteOutcome>('DELETE', `/works/${encodeURIComponent(workId)}`);

export interface AddFileOutcome {
  work: string;
  file: string;
  replaced?: string;
}

export interface CreateWorkOutcome {
  work: string;
  file: string;
  folder: string;
}

export interface MoveFileOutcome {
  work: string;
  target: string;
  file: string;
  workRemoved: boolean;
}

/** Adds a file to a work; with `replace` (a file ID of the work) the old file goes to the trash. `shelf undo` reverts it. */
export const addFile = (workId: string, file: File, options: { replace?: string; carryNotes?: boolean } = {}) =>
  upload<AddFileOutcome>(
    `/works/${encodeURIComponent(workId)}/files${query({ name: file.name, replace: options.replace, carryNotes: options.carryNotes ? 'true' : undefined })}`,
    file,
  );

/** Makes a work of a file, without grouping; what is not given is read from the file. */
export const createWork = (file: File, values: { title?: string; author?: string; year?: string } = {}) =>
  upload<CreateWorkOutcome>(`/works${query({ name: file.name, title: values.title?.trim(), author: values.author?.trim(), year: values.year?.trim() })}`, file);

/** Moves a file to the work `target`, or to a work of its own when `target` is null. `shelf undo` reverts it. */
export const moveFile = (fileId: string, target: string | null) =>
  request<MoveFileOutcome>('POST', `/files/${encodeURIComponent(fileId)}/move`, target === null ? { new: true } : { to: target });

export interface EnrichChange {
  field: string;
  action: 'fill' | 'overwrite';
  before?: string;
  after: string;
}

export interface EnrichKept {
  field: string;
  current?: string;
  online: string;
}

export interface EnrichCandidate {
  number: number;
  title: string;
  authors: string[];
  publisher?: string;
  year?: number;
  isbn13?: string;
}

/** What the online sources know about a work. With several matching books only `candidates` is set. */
export interface EnrichPreview {
  id: string;
  source?: string;
  match?: string;
  changes: EnrichChange[];
  kept: EnrichKept[];
  candidates: EnrichCandidate[];
  warnings: string[];
}

export interface EnrichOutcome {
  id: string;
  source: string;
  fields: string[];
  cover?: string;
  warnings: string[];
}

export interface EnrichOptions {
  overwrite?: boolean;
  pick?: number;
}

export const enrichPreview = (id: string, options: EnrichOptions = {}) =>
  request<EnrichPreview>('POST', `/works/${encodeURIComponent(id)}/enrich`, options);

export const enrichApply = (id: string, options: EnrichOptions = {}) =>
  request<EnrichOutcome>('POST', `/works/${encodeURIComponent(id)}/enrich/apply`, options);

export const splitFile =(fileId: string) => request<SplitOutcome>('POST', `/files/${encodeURIComponent(fileId)}/split`);

export const coverUrl = (id: string) => `/api/v1/works/${encodeURIComponent(id)}/cover`;

export const fileUrl = (id: string, download = false) => `/api/v1/files/${encodeURIComponent(id)}${download ? '?download=true' : ''}`;

/** The Markdown export of a work (F-ANN-7): metadata, highlights and notes; the file is named after the cite key. */
export const exportUrl = (workId: string) => `/api/v1/works/${encodeURIComponent(workId)}/export.md`;

// ---- highlights and notes (F-ANN-1 to F-ANN-4) ----

export type AnnotationColor = 'yellow' | 'green' | 'blue' | 'pink' | 'orange';

/** A highlight (a marked passage, with a color) or a note for a whole file; both can carry a note in Markdown. */
export interface Annotation {
  id: string;
  /** The ID of the file the annotation belongs to. */
  file: string;
  kind: 'highlight' | 'note';
  locator: string | null;
  quote: { exact: string; prefix: string | null; suffix: string | null } | null;
  color: AnnotationColor | null;
  note: string | null;
  /** The marked areas of a highlight in a PDF: x, y, width, height, as fractions of the page. */
  rects: number[][] | null;
  createdAt: string;
  updatedAt: string;
}

export interface AnnotationDraft {
  kind: 'highlight' | 'note';
  locator?: string;
  quote?: { exact: string; prefix?: string; suffix?: string };
  color?: AnnotationColor;
  note?: string;
  rects?: number[][];
}

const fileAnnotationsPath = (fileId: string) => `/files/${encodeURIComponent(fileId)}/annotations`;

export const fileAnnotations = (fileId: string) => request<Annotation[]>('GET', fileAnnotationsPath(fileId));

export const workAnnotations = (workId: string) => request<Annotation[]>('GET', `/works/${encodeURIComponent(workId)}/annotations`);

export const createAnnotation = (fileId: string, draft: AnnotationDraft) =>
  request<Annotation>('POST', fileAnnotationsPath(fileId), draft);

/** Only the note (null clears it) and the color can change. */
export const updateAnnotation = (fileId: string, id: string, changes: { note?: string | null; color?: AnnotationColor }) =>
  request<Annotation>('PATCH', `${fileAnnotationsPath(fileId)}/${encodeURIComponent(id)}`, changes, 'application/merge-patch+json');

export const deleteAnnotation = async (fileId: string, id: string): Promise<void> => {
  const response = await fetch(`/api/v1${fileAnnotationsPath(fileId)}/${encodeURIComponent(id)}`, {
    method: 'DELETE',
    credentials: 'same-origin',
  });
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as Problem | null;
    throw new ApiError(problem?.detail ?? problem?.title ?? `HTTP ${response.status}`, response.status);
  }
};
