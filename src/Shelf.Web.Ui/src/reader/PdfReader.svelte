<script lang="ts">
  import { getDocument, GlobalWorkerOptions } from 'pdfjs-dist';
  import workerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url';
  import { EventBus, PDFFindController, PDFLinkService, PDFViewer } from 'pdfjs-dist/web/pdf_viewer.mjs';
  import 'pdfjs-dist/web/pdf_viewer.css';
  import { fileUrl, type WorkDetail, type WorkFile } from '../api';
  import { replaceHash, router } from '../router.svelte';
  import Entries, { type Entry } from './Entries.svelte';
  import { createProgress } from './progress';
  import ReaderFrame from './ReaderFrame.svelte';

  let { book, file }: { book: WorkDetail; file: WorkFile } = $props();

  GlobalWorkerOptions.workerSrc = workerUrl;

  const PENDING = 3; // FindState.PENDING of the viewer

  let container = $state<HTMLDivElement>();
  let viewerElement = $state<HTMLDivElement>();
  let viewer: InstanceType<typeof PDFViewer> | null = null;
  let bus: InstanceType<typeof EventBus> | null = null;
  let links: InstanceType<typeof PDFLinkService> | null = null;

  let page = $state(1);
  let pages = $state(0);
  let pageInput = $state('1');
  let scaleLabel = $state('Fit width');
  let error = $state<string | null>(null);
  let outline = $state<Entry[]>([]);
  let outlineOpen = $state(false);

  let query = $state('');
  let matches = $state<{ current: number; total: number } | null>(null);
  let finding = $state(false);

  const clamp = (n: number) => Math.min(Math.max(1, n), Math.max(1, pages));

  $effect(() => {
    if (!container || !viewerElement) {
      return;
    }
    const eventBus = new EventBus();
    const linkService = new PDFLinkService({ eventBus });
    const findController = new PDFFindController({ linkService, eventBus });
    const pdfViewer = new PDFViewer({ container, viewer: viewerElement, eventBus, linkService, findController });
    linkService.setViewer(pdfViewer);
    viewer = pdfViewer;
    bus = eventBus;
    links = linkService;

    // The place of the address wins; without one the reader opens where the file was left (F-RDR-4).
    const progress = createProgress(file.id, file.progress?.locator ?? 'page=1');
    let opened = false;

    eventBus.on('pagesinit', () => {
      pages = pdfViewer.pagesCount;
      pdfViewer.currentScaleValue = 'page-width';
      const wanted = /^#page=(\d+)/.exec(router.hash) ?? /^page=(\d+)$/.exec(file.progress?.locator ?? '');
      pdfViewer.currentPageNumber = clamp(wanted ? Number(wanted[1]) : 1);
      page = pdfViewer.currentPageNumber;
      replaceHash(`#page=${page}`);
      opened = true;
      progress.move(`page=${page}`);
    });
    eventBus.on('pagechanging', (event: { pageNumber: number }) => {
      page = event.pageNumber;
      pageInput = String(page);
      replaceHash(`#page=${page}`);
      if (opened) {
        progress.move(`page=${page}`);
      }
    });
    eventBus.on('scalechanging', (event: { scale: number; presetValue?: string }) => {
      scaleLabel = event.presetValue === 'page-width' ? 'Fit width' : `Zoom ${Math.round(event.scale * 100)}%`;
    });
    eventBus.on('updatefindmatchescount', (event: { matchesCount: { current: number; total: number } }) => {
      matches = event.matchesCount;
    });
    eventBus.on('updatefindcontrolstate', (event: { state: number; matchesCount: { current: number; total: number } }) => {
      finding = event.state === PENDING;
      matches = event.matchesCount;
    });

    // The file by its address, with the cookie, in parts: the first page shows before a large file has arrived. The fonts, character
    // maps, color profiles and decoders are part of the build (/pdfjs/), so reading needs no network.
    const task = getDocument({
      url: fileUrl(file.id),
      withCredentials: true,
      standardFontDataUrl: '/pdfjs/standard_fonts/',
      cMapUrl: '/pdfjs/cmaps/',
      cMapPacked: true,
      iccUrl: '/pdfjs/iccs/',
      wasmUrl: '/pdfjs/wasm/',
    });
    task.promise
      .then(async (pdf) => {
        pdfViewer.setDocument(pdf);
        linkService.setDocument(pdf, null);
        const tree = await pdf.getOutline();
        outline = tree ? toEntries(tree) : [];
      })
      .catch((e: Error) => (error = e.message));

    // Fit width follows the room that the page has: a panel that opens, a window that changes.
    const resizing = new ResizeObserver(() => {
      if (scaleLabel === 'Fit width') {
        pdfViewer.currentScaleValue = 'page-width';
      }
    });
    resizing.observe(container);

    return () => {
      progress.close();
      resizing.disconnect();
      void task.destroy();
      pdfViewer.setDocument(null as never);
      viewer = null;
      bus = null;
      links = null;
    };
  });

  type OutlineNode = { title: string; dest: unknown; items: OutlineNode[] };
  function toEntries(nodes: OutlineNode[]): Entry[] {
    return nodes.map((node) => ({
      title: node.title,
      go: () => {
        if (node.dest) {
          void links?.goToDestination(node.dest as never);
        }
      },
      items: toEntries(node.items ?? []),
    }));
  }

  // An address that changes while the reader is open (a link to another place of the same file) moves the page.
  $effect(() => {
    const wanted = /^#page=(\d+)/.exec(router.hash);
    if (wanted && viewer && pages > 0 && clamp(Number(wanted[1])) !== viewer.currentPageNumber) {
      viewer.currentPageNumber = clamp(Number(wanted[1]));
    }
  });

  function goToPage(event: KeyboardEvent): void {
    if (event.key === 'Enter' && viewer && pages > 0) {
      const n = Number(pageInput);
      if (Number.isFinite(n)) {
        viewer.currentPageNumber = clamp(n);
      }
      pageInput = String(viewer.currentPageNumber);
    }
  }

  function find(again: boolean, previous = false): void {
    if (!bus || !viewer || !query.trim()) {
      return;
    }
    bus.dispatch('find', {
      source: viewer,
      type: again ? 'again' : '',
      query: query.trim(),
      caseSensitive: false,
      entireWord: false,
      highlightAll: true,
      findPrevious: previous,
      matchDiacritics: false,
    });
  }

  const searched = $derived(matches !== null && !finding);
  const found = $derived(
    !matches || matches.total === 0 ? '' : matches.total === 1 ? '1 match' : `${matches.current} of ${matches.total} matches`,
  );
</script>

<ReaderFrame {book}>
  {#snippet toolbar()}
    <button type="button" class="button" disabled={page <= 1} onclick={() => viewer?.previousPage()}>Previous page</button>
    <button type="button" class="button" disabled={pages === 0 || page >= pages} onclick={() => viewer?.nextPage()}>Next page</button>
    <input class="page" aria-label="Page" inputmode="numeric" size="3" disabled={pages === 0} bind:value={pageInput} onkeydown={goToPage} />
    <span class="status" role="status">Page {page} of {pages}</span>

    <span class="group">
      <button type="button" class="button" onclick={() => viewer?.decreaseScale()}>Zoom out</button>
      <button type="button" class="button" onclick={() => viewer?.increaseScale()}>Zoom in</button>
      <button type="button" class="button" onclick={() => viewer && (viewer.currentScaleValue = 'page-width')}>Fit width</button>
      <span class="muted">{scaleLabel}</span>
    </span>

    <span class="group">
      <input
        type="search"
        aria-label="Search in this book"
        placeholder="Search in this book"
        disabled={pages === 0}
        bind:value={query}
        onkeydown={(e) => e.key === 'Enter' && find(false)}
      />
      <button type="button" class="button" disabled={!matches?.total} onclick={() => find(true, true)}>Previous match</button>
      <button type="button" class="button" disabled={!matches?.total} onclick={() => find(true)}>Next match</button>
      {#if searched}
        <span class="muted" role="status">{found || `Nothing found for ${query.trim()}`}</span>
      {/if}
    </span>

    {#if outline.length}
      <button type="button" class="button" aria-expanded={outlineOpen} onclick={() => (outlineOpen = !outlineOpen)}>Contents</button>
    {/if}
  {/snippet}

  {#snippet side()}
    {#if outlineOpen && outline.length}
      <aside class="side" aria-label="Contents"><Entries items={outline} /></aside>
    {/if}
  {/snippet}

  {#if error}
    <p class="problem" role="alert">This file could not be opened as a PDF: {error}</p>
  {/if}
  <!-- svelte-ignore a11y_no_noninteractive_tabindex -->
  <div class="pages" bind:this={container} tabindex="0" role="region" aria-label="PDF pages">
    <div class="pdfViewer" bind:this={viewerElement}></div>
  </div>
</ReaderFrame>

<style>
  .pages {
    position: absolute;
    inset: 0;
    overflow: auto;
  }

  .problem {
    margin: 1rem;
  }

  .page {
    width: 3.2rem;
    text-align: center;
  }

  .group {
    display: inline-flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.5rem;
  }

  .side {
    flex: 0 0 18rem;
    max-width: 40%;
    overflow: auto;
    padding: 0.75rem;
    background: var(--surface);
    border-right: 1px solid var(--line);
  }
</style>
