<script lang="ts">
  import 'foliate-js/view.js';
  import { Overlayer } from 'foliate-js/overlayer.js';
  import { fileUrl, type Annotation, type AnnotationColor, type WorkDetail, type WorkFile } from '../api';
  import { replaceHash, router } from '../router.svelte';
  import { colors, createAnnotations } from './annotations.svelte';
  import Entries, { type Entry } from './Entries.svelte';
  import BookmarkButton from './BookmarkButton.svelte';
  import NotesPanel from './NotesPanel.svelte';
  import SelectionBar from './SelectionBar.svelte';
  import { createProgress } from './progress';
  import ReaderFrame from './ReaderFrame.svelte';

  let { book, file }: { book: WorkDetail; file: WorkFile } = $props();

  type Theme = 'light' | 'dark' | 'sepia';
  const themes: Record<Theme, { label: string; background: string; color: string; link: string }> = {
    light: { label: 'Light', background: '#fbfaf7', color: '#1a2220', link: '#2f5d50' },
    dark: { label: 'Dark', background: '#161a1b', color: '#e3e8e4', link: '#8dc2af' },
    sepia: { label: 'Sepia', background: '#f4ecd8', color: '#433422', link: '#6b4a1e' },
  };
  const sizes = { min: 70, max: 200, step: 10 };

  interface Hit {
    cfi: string;
    excerpt: { pre: string; match: string; post: string };
  }
  interface HitGroup {
    label: string;
    subitems: Hit[];
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let view: any = null;
  let host = $state<HTMLDivElement>();

  let loading = $state(true);
  let error = $state<string | null>(null);
  let percent = $state(0);
  let lastCfi = '';
  // The place of the reader as a state, for the bookmark button; `lastCfi` is read in handlers only.
  let here = $state<string | null>(null);
  let contents = $state<Entry[]>([]);
  let contentsOpen = $state(false);
  let size = $state(100);
  let theme = $state<Theme>(matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');

  let query = $state('');
  let searching = $state(false);
  let searched = $state(false);
  let groups = $state<HitGroup[]>([]);
  let searchOpen = $state(false);

  // Highlights and notes (F-ANN-1, F-ANN-2). `pending` is the passage that is selected in the book right now.
  interface Pending {
    doc: Document;
    cfi: string;
    text: string;
    prefix?: string;
    suffix?: string;
  }
  const annotations = createAnnotations(() => ({ file: file.id }));
  let notesOpen = $state(false);
  let pending = $state<Pending | null>(null);
  let markerColor = $state<AnnotationColor>('yellow');
  let selectedNote = $state<string | null>(null);
  // The highlights that are drawn in the pages that are open (what `data-highlights` of the book reports).
  const drawn = new Set<string>();
  let drawnCount = $state(0);

  // Reflow, spacing and the colors of the page are set from here; the book's own colors give way (the theme is the reader's choice).
  function styles(): string {
    const t = themes[theme];
    return `
      @namespace epub "http://www.idpf.org/2007/ops";
      html { font-size: ${size}% !important; }
      html, body { background: ${t.background} !important; color: ${t.color} !important; }
      a:link, a:visited { color: ${t.link} !important; }
      p, li, blockquote, dd { line-height: 1.6; hanging-punctuation: allow-end last; widows: 2; }
      pre { white-space: pre-wrap !important; }
      aside[epub|type~="endnote"], aside[epub|type~="footnote"], aside[epub|type~="note"], aside[epub|type~="rearnote"] { display: none; }
    `;
  }

  $effect(() => {
    // Whenever the size or the theme changes.
    const css = styles();
    view?.renderer?.setStyles?.(css);
  });

  $effect(() => {
    if (!host) {
      return;
    }
    let current = true;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const element = document.createElement('foliate-view') as any;
    element.style.position = 'absolute';
    element.style.inset = '0';
    host.append(element);
    // Where the file was left (F-RDR-4); the place of the address wins over it.
    const progress = createProgress(file.id, file.progress?.locator ?? null);

    (async () => {
      const response = await fetch(fileUrl(file.id), { credentials: 'same-origin' });
      if (!response.ok) {
        throw new Error(`the server answered ${response.status}`);
      }
      const blob = await response.blob();
      await element.open(new File([blob], file.name, { type: 'application/epub+zip' }));
      if (!current) {
        return;
      }
      view = element;
      let wanted: string | undefined;
      try {
        wanted = /^#cfi=(.+)$/.exec(decodeURIComponent(router.hash))?.[1];
      } catch {
        wanted = undefined;
      }
      const resume = wanted ?? file.progress?.locator;
      let first = true;
      element.addEventListener('relocate', (event: Event) => {
        const detail = (event as CustomEvent<{ cfi: string; fraction: number }>).detail;
        percent = Math.round(detail.fraction * 100);
        lastCfi = detail.cfi;
        here = detail.cfi;
        replaceHash(`#cfi=${detail.cfi}`);
        // Opening where the file was left, or at its start, is not a move.
        if (first && !wanted) {
          progress.start(detail.cfi);
        } else {
          progress.move(detail.cfi);
        }
        first = false;
      });
      element.addEventListener('load', (event: Event) => {
        const { doc, index } = (event as CustomEvent<{ doc: Document; index: number }>).detail;
        watchSelection(doc, index);
      });
      element.addEventListener('create-overlay', (event: Event) => void drawSection((event as CustomEvent<{ index: number }>).detail.index));
      element.addEventListener('draw-annotation', (event: Event) => {
        const { draw, annotation } = (event as CustomEvent<{ draw: (f: unknown, o: unknown) => void; annotation: { value: string; color: AnnotationColor } }>).detail;
        draw(Overlayer.highlight, { color: colors[annotation.color] ?? colors.yellow });
        drawn.add(annotation.value);
        drawnCount = drawn.size;
      });
      element.addEventListener('show-annotation', (event: Event) => {
        const value = (event as CustomEvent<{ value: string }>).detail.value;
        const found = annotations.items.find((a) => a.locator === value);
        if (found) {
          selectedNote = found.id;
          openNotes();
        }
      });
      element.renderer.setStyles(styles());
      contents = toEntries(element.book.toc ?? []);
      // The highlights are known before the first page is drawn, so that it is drawn with them.
      await annotations.load();
      await element.init({ lastLocation: resume ?? null, showTextStart: !resume });
      loading = false;
    })().catch((e: Error) => {
      if (current) {
        error = e.message;
        loading = false;
      }
    });

    return () => {
      current = false;
      progress.close();
      view = null;
      element.close?.();
      element.remove();
    };
  });

  type TocNode = { label: string; href: string; subitems?: TocNode[] };
  function toEntries(nodes: TocNode[]): Entry[] {
    return nodes.map((node) => ({
      title: node.label,
      go: () => void view?.goTo(node.href),
      items: toEntries(node.subitems ?? []),
    }));
  }

  /** The index of the section that holds a location; null for a location that does not resolve. */
  async function sectionOf(cfi: string): Promise<number | null> {
    try {
      return (await Promise.resolve(view?.resolveCFI(cfi)))?.index ?? null;
    } catch {
      return null;
    }
  }

  async function drawSection(index: number): Promise<void> {
    for (const a of annotations.items) {
      if (a.kind === 'highlight' && a.locator && (await sectionOf(a.locator)) === index) {
        void view?.addAnnotation({ value: a.locator, color: a.color });
      }
    }
  }

  // A selection in a page of the book (made with the mouse or the keyboard) is kept until it is marked or the selection goes.
  function watchSelection(doc: Document, index: number): void {
    doc.addEventListener('selectionchange', () => {
      const selection = doc.getSelection();
      if (!selection || selection.isCollapsed || selection.rangeCount === 0 || !view) {
        if (pending?.doc === doc) {
          pending = null;
        }
        return;
      }
      const range = selection.getRangeAt(0);
      const text = selection.toString().trim().slice(0, 20_000);
      if (!text) {
        return;
      }
      const start = range.startContainer;
      const end = range.endContainer;
      const prefix = start.nodeType === Node.TEXT_NODE ? (start.textContent ?? '').slice(Math.max(0, range.startOffset - 40), range.startOffset) : '';
      const suffix = end.nodeType === Node.TEXT_NODE ? (end.textContent ?? '').slice(range.endOffset, range.endOffset + 40) : '';
      pending = { doc, cfi: view.getCFI(index, range), text, prefix: prefix || undefined, suffix: suffix || undefined };
    });
  }

  async function highlight(): Promise<void> {
    const marked = pending;
    if (!marked || !view) {
      return;
    }
    pending = null;
    marked.doc.getSelection()?.removeAllRanges();
    const created = await annotations.add(file.id, {
      kind: 'highlight',
      locator: marked.cfi,
      quote: { exact: marked.text, prefix: marked.prefix, suffix: marked.suffix },
      color: markerColor,
    });
    if (created?.locator) {
      await view?.addAnnotation({ value: created.locator, color: created.color });
    }
  }

  function goTo(annotation: Annotation): void {
    if (annotation.locator) {
      void view?.goTo(annotation.locator);
    }
  }

  function recolored(annotation: Annotation): void {
    if (annotation.locator) {
      void view?.addAnnotation({ value: annotation.locator, color: annotation.color });
    }
  }

  function removed(annotation: Annotation): void {
    if (annotation.locator) {
      void view?.deleteAnnotation({ value: annotation.locator });
      drawn.delete(annotation.locator);
      drawnCount = drawn.size;
    }
  }

  function openNotes(): void {
    notesOpen = true;
    contentsOpen = false;
  }

  async function search(): Promise<void> {
    const text = query.trim();
    if (!view || !text) {
      return;
    }
    groups = [];
    searching = true;
    searched = false;
    searchOpen = true;
    try {
      for await (const result of view.search({ query: text })) {
        if (result !== 'done' && result.subitems) {
          groups = [...groups, result as HitGroup];
        }
      }
    } finally {
      searching = false;
      searched = true;
    }
  }

  // The paginator ignores a turn while it still settles the last one (about a tenth of a second after a section changed). A click
  // that came too early is repeated a few times until the position moves or the book ends.
  async function turn(forward: boolean): Promise<void> {
    for (let attempt = 0; attempt < 4 && view; attempt++) {
      const before = lastCfi;
      await (forward ? view.next() : view.prev());
      await new Promise((resolve) => setTimeout(resolve, 150));
      if (lastCfi !== before) {
        return;
      }
    }
  }

  const hits = $derived(groups.reduce((n, g) => n + g.subitems.length, 0));
  const found = $derived(hits === 1 ? '1 match' : `${hits} matches`);

  const entries = $derived(notesOpen ? 'notes' : searchOpen && !contentsOpen ? 'search' : contentsOpen ? 'contents' : 'none');
</script>

<ReaderFrame {book} background={themes[theme].background}>
  {#snippet toolbar()}
    <button type="button" class="button" disabled={loading} onclick={() => turn(false)}>Previous page</button>
    <button type="button" class="button" disabled={loading} onclick={() => turn(true)}>Next page</button>
    <span class="status" role="status">{percent}%</span>

    <span class="group">
      <button type="button" class="button" disabled={size <= sizes.min} onclick={() => (size -= sizes.step)}>Smaller text</button>
      <button type="button" class="button" disabled={size >= sizes.max} onclick={() => (size += sizes.step)}>Larger text</button>
      <span class="muted">Text {size}%</span>
    </span>

    <span class="group" role="group" aria-label="Theme">
      {#each Object.entries(themes) as [key, t] (key)}
        <button type="button" class="button" aria-pressed={theme === key} onclick={() => (theme = key as Theme)}>{t.label}</button>
      {/each}
    </span>

    <span class="group">
      <input
        type="search"
        aria-label="Search in this book"
        placeholder="Search in this book"
        disabled={loading}
        bind:value={query}
        onkeydown={(e) => e.key === 'Enter' && search()}
      />
      {#if searching}<span class="muted" role="status">Searching…</span>{:else if searched}<span class="muted" role="status">{hits ? found : `Nothing found for ${query.trim()}`}</span>{/if}
    </span>

    {#if contents.length}
      <button
        type="button"
        class="button"
        aria-expanded={contentsOpen}
        onclick={() => {
          contentsOpen = !contentsOpen;
          notesOpen = false;
        }}>Contents</button
      >
    {/if}

    <BookmarkButton store={annotations} fileId={file.id} locator={loading ? null : here} />
    <button type="button" class="button" aria-expanded={notesOpen} disabled={loading} onclick={() => (notesOpen ? (notesOpen = false) : openNotes())}>Notes</button>

    {#if pending}
      <SelectionBar bind:color={markerColor} onhighlight={highlight} />
    {/if}
  {/snippet}

  {#snippet side()}
    {#if entries === 'notes'}
      <NotesPanel store={annotations} fileId={file.id} go={goTo} onrecolor={recolored} onremove={removed} selected={selectedNote} />
    {:else if entries === 'contents'}
      <aside class="side" aria-label="Contents"><Entries items={contents} /></aside>
    {:else if entries === 'search' && hits}
      <aside class="side" aria-label="Search results">
        {#each groups as group, g (g)}
          <h3>{group.label}</h3>
          <ul>
            {#each group.subitems as hit, i (i)}
              <li>
                <button type="button" onclick={() => view?.goTo(hit.cfi)}>{hit.excerpt.pre}<mark>{hit.excerpt.match}</mark>{hit.excerpt.post}</button>
              </li>
            {/each}
          </ul>
        {/each}
      </aside>
    {/if}
  {/snippet}

  {#if error}
    <div class="problem">
      <p role="alert">This file could not be opened as an EPUB: {error}</p>
      <p><a class="button" href={fileUrl(file.id, true)} download aria-label={`Download ${file.name}`}>Download</a></p>
    </div>
  {:else if loading}
    <p class="problem muted">Opening the book…</p>
  {/if}
  <div class="host" bind:this={host} aria-label="Book" role="document" data-highlights={drawnCount}></div>
</ReaderFrame>

<style>
  .host {
    position: absolute;
    inset: 0;
  }

  .problem {
    position: relative;
    z-index: 1;
    margin: 1rem;
  }

  .group {
    display: inline-flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.5rem;
  }

  .group[role='group'] .button[aria-pressed='true'] {
    background: var(--accent);
    border-color: var(--accent);
    color: var(--accent-ink);
  }

  .side {
    flex: 0 0 18rem;
    max-width: 40%;
    overflow: auto;
    padding: 0.75rem;
    background: var(--surface);
    border-right: 1px solid var(--line);
  }

  .side h3 {
    margin: 0.5rem 0 0.25rem;
  }

  .side ul {
    list-style: none;
    margin: 0;
    padding: 0;
  }

  .side li button {
    display: block;
    width: 100%;
    text-align: left;
    background: transparent;
    border: 0;
    border-radius: var(--radius);
    padding: 0.3rem 0.5rem;
    font-family: var(--serif);
  }

  .side li button:hover {
    background: var(--paper);
  }
</style>
