import {
  createAnnotation,
  deleteAnnotation,
  fileAnnotations,
  updateAnnotation,
  workAnnotations,
  type Annotation,
  type AnnotationColor,
  type AnnotationDraft,
} from '../api';

/**
 * The highlights and notes of one file (the reader) or of all files of a work (the book page), with the changes a person can make.
 * A failure is kept in `error` and shown by the view; the list stays as the server last said.
 */
export function createAnnotations(scopeOf: () => { file: string } | { work: string }) {
  let items = $state<Annotation[]>([]);
  let error = $state<string | null>(null);
  let loaded = $state(false);

  async function attempt<T>(action: () => Promise<T>): Promise<T | undefined> {
    try {
      error = null;
      return await action();
    } catch (e) {
      error = (e as Error).message;
      return undefined;
    }
  }

  return {
    get items() {
      return items;
    },
    get error() {
      return error;
    },
    get loaded() {
      return loaded;
    },
    async load(): Promise<void> {
      const scope = scopeOf();
      const list = await attempt(() => ('file' in scope ? fileAnnotations(scope.file) : workAnnotations(scope.work)));
      if (list) {
        items = list;
      }
      loaded = true;
    },
    async add(fileId: string, draft: AnnotationDraft): Promise<Annotation | undefined> {
      const created = await attempt(() => createAnnotation(fileId, draft));
      if (created) {
        items = [...items, created];
      }
      return created;
    },
    async changeNote(annotation: Annotation, note: string | null): Promise<boolean> {
      const updated = await attempt(() => updateAnnotation(annotation.file, annotation.id, { note }));
      if (updated) {
        items = items.map((a) => (a.id === updated.id ? updated : a));
      }
      return updated !== undefined;
    },
    async changeColor(annotation: Annotation, color: AnnotationColor): Promise<Annotation | undefined> {
      const updated = await attempt(() => updateAnnotation(annotation.file, annotation.id, { color }));
      if (updated) {
        items = items.map((a) => (a.id === updated.id ? updated : a));
      }
      return updated;
    },
    async remove(annotation: Annotation): Promise<boolean> {
      const done = await attempt(async () => {
        await deleteAnnotation(annotation.file, annotation.id);
        return true;
      });
      if (done) {
        items = items.filter((a) => a.id !== annotation.id);
      }
      return done === true;
    },
  };
}

export type Annotations = ReturnType<typeof createAnnotations>;

/** The colors of the marker, as the reader draws them (the page background stays readable under them). */
export const colors: Record<AnnotationColor, string> = {
  yellow: '#e9c46a',
  green: '#7fb685',
  blue: '#6ea8d9',
  pink: '#e58fb0',
  orange: '#e8955a',
};
