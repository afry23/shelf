<script lang="ts">
  import { ApiError, editWork, fileUrl, mergeWorks, setTags, similar as loadSimilar, works as findWorks, tags as loadTags, work as loadWork, type ReadStatus, type SearchItem, type TagNode, type WorkDetail, type WorkItem } from '../api';
  import Cover from '../components/Cover.svelte';
  import Segmented from '../components/Segmented.svelte';
  import { authorLine, bytes, readLink, resumeText } from '../format';

  let { id }: { id: string } = $props();

  let book = $state<WorkDetail | null>(null);
  let missing = $state(false);
  let loadError = $state<string | null>(null);
  let actionError = $state<string | null>(null);
  let suggestions = $state<string[]>([]);
  let newTag = $state('');

  $effect(() => {
    const wanted = id;
    let current = true;
    book = null;
    missing = false;
    loadError = null;
    editing = false;
    loadWork(wanted)
      .then((result) => {
        if (current) {
          book = result;
        }
      })
      .catch((e: Error) => {
        if (current) {
          missing = e instanceof ApiError && e.status === 404;
          loadError = missing ? null : e.message;
        }
      });
    return () => {
      current = false;
    };
  });

  // The works most like this one. Without embeddings the list is empty and the section stays away: the view needs nothing else.
  let similarBooks = $state<SearchItem[]>([]);

  $effect(() => {
    const wanted = id;
    let current = true;
    similarBooks = [];
    loadSimilar(wanted)
      .then((result) => {
        if (current) {
          similarBooks = result.results;
        }
      })
      .catch(() => {
        if (current) {
          similarBooks = [];
        }
      });
    return () => {
      current = false;
    };
  });

  // The tags of the library, for the suggestions of the field.
  $effect(() => {
    const flatten = (nodes: TagNode[]): string[] => nodes.flatMap((n) => [n.path, ...flatten(n.children)]);
    loadTags()
      .then((tree) => (suggestions = flatten(tree.roots)))
      .catch(() => (suggestions = []));
  });

  const readFile = $derived(book?.files.find((f) => f.id === book?.readFile) ?? book?.files[0]);
  // The table of contents belongs to one file; its locations say which kind: epubcfi(...) for an EPUB, page=N for a PDF.
  const tocFile = $derived(
    book?.toc.find((t) => t.location)?.location?.startsWith('epubcfi(')
      ? book.files.find((f) => f.format === 'epub')
      : book?.files.find((f) => f.format === 'pdf') ?? book?.files[0],
  );

  async function patch(changes: Record<string, unknown>): Promise<boolean> {
    if (!book) {
      return false;
    }
    actionError = null;
    try {
      const updated = await editWork(book.id, changes);
      book = { ...updated, toc: book.toc };
      return true;
    } catch (e) {
      actionError = (e as Error).message;
      return false;
    }
  }

  const setStatus = (status: ReadStatus) => patch({ readStatus: status });
  const setRating = (stars: number) => patch({ rating: book?.rating === stars ? null : stars });

  async function changeTags(next: string[]): Promise<void> {
    if (!book) {
      return;
    }
    actionError = null;
    try {
      const result = await setTags(book.id, next);
      book = { ...book, tags: result.tags };
      newTag = '';
    } catch (e) {
      actionError = (e as Error).message;
    }
  }

  function addTag(event: KeyboardEvent): void {
    if (event.key !== 'Enter' || !book) {
      return;
    }
    event.preventDefault();
    const name = newTag.trim();
    if (name) {
      void changeTags([...book.tags, name]);
    }
  }

  // ---- merging another work into this one ----

  let mergeQuery = $state('');
  let mergeFound = $state<WorkItem[] | null>(null);
  // The work that waits for a second click: nothing moves before.
  let mergeCandidate = $state<WorkItem | null>(null);
  let mergeDone = $state<string | null>(null);

  $effect(() => {
    const q = mergeQuery.trim();
    const self = id;
    mergeCandidate = null;
    if (!q) {
      mergeFound = null;
      return;
    }
    let current = true;
    const timer = setTimeout(() => {
      findWorks({ q: `title:"${q.replaceAll('"', '')}"`, limit: 6 })
        .then((list) => {
          if (current) {
            mergeFound = list.items.filter((w) => w.id !== self);
          }
        })
        .catch(() => {
          if (current) {
            mergeFound = [];
          }
        });
    }, 200);
    return () => {
      current = false;
      clearTimeout(timer);
    };
  });

  async function mergeIn(source: WorkItem): Promise<void> {
    if (!book) {
      return;
    }
    actionError = null;
    try {
      const outcome = await mergeWorks(book.id, source.id);
      mergeDone = `Merged ${source.title} into this book: ${outcome.movedFiles} ${outcome.movedFiles === 1 ? 'file' : 'files'} moved. This cannot be undone.`;
      mergeQuery = '';
      mergeFound = null;
      mergeCandidate = null;
      book = { ...(await loadWork(book.id)) };
    } catch (e) {
      actionError = (e as Error).message;
    }
  }

  // ---- editing the details ----

  let editing = $state(false);
  let saveError = $state<string | null>(null);
  let draft = $state({
    title: '',
    subtitle: '',
    authors: '',
    language: '',
    year: '',
    publisher: '',
    series: '',
    seriesIndex: '',
    edition: '',
    isbn: '',
    description: '',
  });

  function startEditing(): void {
    if (!book) {
      return;
    }
    draft = {
      title: book.title,
      subtitle: book.subtitle ?? '',
      authors: book.authors.join('\n'),
      language: book.language ?? '',
      year: book.year?.toString() ?? '',
      publisher: book.publisher ?? '',
      series: book.series ?? '',
      seriesIndex: book.seriesIndex?.toString() ?? '',
      edition: book.edition ?? '',
      isbn: book.isbn13 ?? '',
      description: book.description ?? '',
    };
    saveError = null;
    editing = true;
  }

  // A number as a number, so that the server checks its range; anything else as it is, so that the server says what is wrong.
  const asNumber = (text: string): number | string | null => {
    const trimmed = text.trim();
    return trimmed === '' ? null : /^-?\d+(\.\d+)?$/.test(trimmed) ? Number(trimmed) : trimmed;
  };
  const asText = (text: string): string | null => (text.trim() === '' ? null : text.trim());

  async function save(event: SubmitEvent): Promise<void> {
    event.preventDefault();
    if (!book) {
      return;
    }
    const changes: Record<string, unknown> = {};
    const compare = (key: string, was: string | null | undefined, now: string) => {
      if ((was ?? '') !== now.trim()) {
        changes[key] = key === 'title' ? now : asText(now);
      }
    };
    compare('title', book.title, draft.title);
    compare('subtitle', book.subtitle, draft.subtitle);
    compare('language', book.language, draft.language);
    compare('publisher', book.publisher, draft.publisher);
    compare('series', book.series, draft.series);
    compare('edition', book.edition, draft.edition);
    compare('isbn', book.isbn13, draft.isbn);
    compare('description', book.description, draft.description);
    if ((book.year?.toString() ?? '') !== draft.year.trim()) {
      changes.year = asNumber(draft.year);
    }
    if ((book.seriesIndex?.toString() ?? '') !== draft.seriesIndex.trim()) {
      changes.seriesIndex = asNumber(draft.seriesIndex);
    }
    const authors = draft.authors.split('\n').map((a) => a.trim());
    if (authors.join('\n') !== book.authors.join('\n')) {
      changes.authors = authors.filter((a) => a !== '');
    }

    if (Object.keys(changes).length === 0) {
      editing = false;
      return;
    }
    saveError = null;
    try {
      const updated = await editWork(book.id, changes);
      book = { ...updated, toc: book.toc };
      editing = false;
    } catch (e) {
      saveError = (e as Error).message;
    }
  }

  const fields: { key: keyof typeof draft; label: string; wide?: boolean }[] = [
    { key: 'title', label: 'Title', wide: true },
    { key: 'subtitle', label: 'Subtitle', wide: true },
    { key: 'language', label: 'Language' },
    { key: 'year', label: 'Year' },
    { key: 'publisher', label: 'Publisher' },
    { key: 'edition', label: 'Edition' },
    { key: 'series', label: 'Series' },
    { key: 'seriesIndex', label: 'Series number' },
    { key: 'isbn', label: 'ISBN' },
  ];
</script>

{#if missing}
  <div class="page">
    <section class="notice">
      <h1>This book is not in the library</h1>
      <p><a href="/library">Back to the library</a></p>
    </section>
  </div>
{:else if loadError}
  <div class="page"><p role="alert">{loadError}</p></div>
{:else if !book}
  <div class="page"><p class="muted">Loading…</p></div>
{:else}
  <div class="page">
    <div class="work">
      <aside class="side">
        <div class="cover-box"><Cover id={book.id} title={book.title} authors={book.authors} hasCover={book.hasCover} /></div>
        {#if readFile}
          <a class="button primary read" href={readLink(readFile.id)}>{readFile.progress ? 'Continue reading' : 'Read'}</a>
          {#if readFile.progress}
            <p class="muted resume">{resumeText(readFile.progress.locator)}</p>
          {/if}
        {/if}
        <div class="control">
          <Segmented
            label="Reading status"
            options={[
              { value: 'unread', label: 'Unread' },
              { value: 'reading', label: 'Reading' },
              { value: 'read', label: 'Finished' },
            ]}
            value={book.readStatus}
            onchange={setStatus}
          />
        </div>
        <div class="stars" role="group" aria-label="Rating">
          {#each [1, 2, 3, 4, 5] as stars (stars)}
            <button
              type="button"
              class:on={(book.rating ?? 0) >= stars}
              aria-pressed={book.rating === stars}
              aria-label={stars === 1 ? '1 star' : `${stars} stars`}
              onclick={() => setRating(stars)}>★</button
            >
          {/each}
        </div>
      </aside>

      <div class="body">
        <header>
          <h1>{book.title}</h1>
          {#if book.subtitle}<p class="subtitle">{book.subtitle}</p>{/if}
          <p class="authors">{authorLine(book.authors)}</p>
        </header>

        {#if actionError}<p role="alert">{actionError}</p>{/if}

        {#if editing}
          <form class="edit" onsubmit={save}>
            {#if saveError}<p role="alert">{saveError}</p>{/if}
            {#each fields as field (field.key)}
              <label class:wide={field.wide}>
                <span>{field.label}</span>
                <input type="text" bind:value={draft[field.key]} />
              </label>
            {/each}
            <label class="wide">
              <span>Authors (one to a line)</span>
              <textarea rows="3" bind:value={draft.authors}></textarea>
            </label>
            <label class="wide">
              <span>Description</span>
              <textarea rows="5" bind:value={draft.description}></textarea>
            </label>
            <div class="actions">
              <button type="submit" class="button primary">Save</button>
              <button type="button" class="button" onclick={() => (editing = false)}>Cancel</button>
            </div>
          </form>
        {:else}
          {#if book.description}<p class="description">{book.description}</p>{/if}
          <dl class="facts">
            {#if book.year}<dt>Year</dt><dd>{book.year}</dd>{/if}
            {#if book.language}<dt>Language</dt><dd>{book.language}</dd>{/if}
            {#if book.publisher}<dt>Publisher</dt><dd>{book.publisher}</dd>{/if}
            {#if book.series}<dt>Series</dt><dd>{book.series}{book.seriesIndex !== null ? ` ${book.seriesIndex}` : ''}</dd>{/if}
            {#if book.edition}<dt>Edition</dt><dd>{book.edition}</dd>{/if}
            {#if book.isbn13}<dt>ISBN</dt><dd>{book.isbn13}</dd>{/if}
            <dt>Work ID</dt><dd class="work-id">{book.id}</dd>
          </dl>
          <p><button type="button" class="button" onclick={startEditing}>Edit details</button></p>
        {/if}

        <section>
          <h2>Tags</h2>
          <ul class="tags" aria-label="Tags">
            {#each book.tags as tag (tag)}
              <li>
                <span>{tag}</span>
                <button type="button" aria-label={`Remove tag ${tag}`} onclick={() => changeTags(book!.tags.filter((t) => t !== tag))}>×</button>
              </li>
            {/each}
          </ul>
          <input class="tag-input" type="text" aria-label="Add a tag" list="tag-suggestions" placeholder="Add a tag" bind:value={newTag} onkeydown={addTag} />
          <datalist id="tag-suggestions">
            {#each suggestions as tag (tag)}<option value={tag}></option>{/each}
          </datalist>
        </section>

        <section>
          <h2>Files</h2>
          <table class="files">
            <thead><tr><th scope="col">Name</th><th scope="col">Format</th><th scope="col">Size</th><th scope="col">Pages</th><th scope="col"><span class="visually-hidden">Download</span></th></tr></thead>
            <tbody>
              {#each book.files as file (file.id)}
                <tr>
                  <td class="name">{file.name}</td>
                  <td>{file.format}</td>
                  <td>{bytes(file.sizeBytes)}</td>
                  <td>{file.pages ?? ''}</td>
                  <td><a href={fileUrl(file.id, true)} download aria-label={`Download ${file.name}`}>Download</a></td>
                </tr>
              {/each}
            </tbody>
          </table>
        </section>

        <section>
          <h2>Merge another work</h2>
          {#if mergeDone}<p class="done" role="status">{mergeDone}</p>{/if}
          <input type="search" aria-label="Find a work to merge in" placeholder="Find a work to merge in" bind:value={mergeQuery} />
          {#if mergeFound}
            {#if mergeFound.length === 0}
              <p class="muted">No other work found.</p>
            {:else}
              <ul class="similar" aria-label="Works to merge in">
                {#each mergeFound as item (item.id)}
                  <li>
                    <a href={`/works/${item.id}`}>{item.title}</a>
                    <span class="muted">{authorLine(item.authors)}{item.year ? ` (${item.year})` : ''}</span>
                    {#if mergeCandidate?.id === item.id}
                      <p>Merge {item.title} into {book.title}? Its files move here and its folder goes. A merge cannot be undone with shelf undo.</p>
                      <button type="button" class="button danger" onclick={() => mergeIn(item)}>Confirm</button>
                      <button type="button" class="button" onclick={() => (mergeCandidate = null)}>Cancel</button>
                    {:else}
                      <button type="button" class="button" onclick={() => (mergeCandidate = item)}>Merge into this book</button>
                    {/if}
                  </li>
                {/each}
              </ul>
            {/if}
          {/if}
        </section>

        {#if similarBooks.length}
          <section>
            <h2>Similar books</h2>
            <ul class="similar" aria-label="Similar books">
              {#each similarBooks as item (item.id)}
                <li>
                  <a href={`/works/${item.id}`}>{item.title}</a>
                  <span class="muted">{authorLine(item.authors)}{item.year ? ` (${item.year})` : ''}</span>
                </li>
              {/each}
            </ul>
          </section>
        {/if}

        {#if book.toc.length && tocFile}
          <section>
            <h2>Contents</h2>
            <nav aria-label="Table of contents">
              <ol class="toc">
                {#each book.toc as entry, index (index)}
                  <li style:padding-left="{Math.max(0, entry.level - 1) * 1.25}rem">
                    {#if entry.location}
                      <a href={readLink(tocFile.id, entry.location)}>{entry.title}</a>
                    {:else}
                      <span>{entry.title}</span>
                    {/if}
                  </li>
                {/each}
              </ol>
            </nav>
          </section>
        {/if}
      </div>
    </div>
  </div>
{/if}

<style>
  .done {
    margin: 0.5rem 0;
    border-left: 3px solid var(--accent);
    background: var(--surface);
    padding: 0.6rem 0.85rem;
  }

  .page {
    max-width: 62rem;
    margin: 0 auto;
    padding: 1.5rem var(--gutter) 4rem;
  }

  .notice {
    padding: 3rem 0;
  }

  .notice h1 {
    margin-bottom: 0.75rem;
  }

  .work {
    display: grid;
    grid-template-columns: 13rem minmax(0, 1fr);
    gap: 2.5rem;
    align-items: start;
  }

  .side {
    display: grid;
    grid-template-columns: minmax(0, 1fr);
    gap: 1rem;
    justify-items: start;
  }

  .control {
    width: 100%;
  }

  .control :global(.segmented) {
    display: flex;
  }

  .control :global(.segmented button) {
    flex: 1;
    min-width: 0;
    padding-inline: 0.25rem;
  }

  .cover-box {
    width: 100%;
    padding-bottom: 0.5rem;
    position: relative;
  }

  .cover-box::after {
    content: '';
    position: absolute;
    left: -0.5rem;
    right: -0.5rem;
    bottom: 0;
    height: 0.5rem;
    background: linear-gradient(var(--shelf), var(--shelf-edge));
  }

  .resume {
    margin: 0;
    font-size: 0.875rem;
    text-align: center;
  }

  .read {
    width: 100%;
    justify-content: center;
    font-weight: 600;
  }

  .stars {
    display: flex;
    gap: 0.1rem;
  }

  .stars button {
    background: transparent;
    border: 0;
    padding: 0.1rem 0.15rem;
    font-size: 1.4rem;
    line-height: 1;
    color: var(--line);
  }

  .stars button.on {
    color: var(--accent);
  }

  .body {
    display: grid;
    gap: 1.5rem;
    min-width: 0;
  }

  .subtitle {
    font-family: var(--serif);
    font-size: 1.15rem;
    color: var(--muted);
    margin-top: 0.25rem;
  }

  .authors {
    margin-top: 0.4rem;
    font-family: var(--serif);
    font-size: 1.05rem;
  }

  .description {
    max-width: 38rem;
    font-family: var(--serif);
    line-height: 1.65;
  }

  .facts {
    display: grid;
    grid-template-columns: max-content 1fr;
    gap: 0.25rem 1.25rem;
    margin: 0;
  }

  .facts dt {
    color: var(--muted);
  }

  .facts dd {
    margin: 0;
  }

  .facts .work-id {
    font-family: ui-monospace, monospace;
    overflow-wrap: anywhere;
    user-select: all;
  }

  section h2 {
    margin-bottom: 0.6rem;
  }

  .tags {
    display: flex;
    flex-wrap: wrap;
    gap: 0.4rem;
    list-style: none;
    margin: 0 0 0.6rem;
    padding: 0;
  }

  .tags li {
    display: inline-flex;
    align-items: center;
    border: 1px solid var(--line);
    background: var(--surface);
    border-radius: 999px;
    padding: 0.1rem 0.25rem 0.1rem 0.75rem;
    gap: 0.25rem;
  }

  .tags button {
    background: transparent;
    border: 0;
    color: var(--muted);
    padding: 0 0.35rem;
    font-size: 1.1rem;
    line-height: 1.4;
  }

  .tags button:hover {
    color: var(--danger);
  }

  .tag-input {
    width: min(16rem, 100%);
  }

  .files {
    width: 100%;
    border-collapse: collapse;
  }

  .files th,
  .files td {
    text-align: left;
    padding: 0.4rem 0.75rem 0.4rem 0;
    border-bottom: 1px solid var(--line);
    font-weight: normal;
  }

  .files th {
    color: var(--muted);
  }

  .files .name {
    overflow-wrap: anywhere;
  }

  .similar {
    list-style: none;
    margin: 0;
    padding: 0;
    display: grid;
    gap: 0.4rem;
  }

  .similar li {
    display: flex;
    flex-wrap: wrap;
    gap: 0.25rem 0.75rem;
    align-items: baseline;
  }

  .toc {
    list-style: none;
    margin: 0;
    padding: 0;
    display: grid;
    gap: 0.2rem;
    font-family: var(--serif);
  }

  .edit {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 0.85rem 1rem;
  }

  .edit label {
    display: grid;
    gap: 0.25rem;
  }

  .edit .wide,
  .edit [role='alert'],
  .edit .actions {
    grid-column: 1 / -1;
  }

  .edit label span {
    color: var(--muted);
    font-size: 0.875rem;
  }

  .actions {
    display: flex;
    gap: 0.6rem;
  }

  @media (max-width: 700px) {
    .work {
      grid-template-columns: minmax(0, 1fr);
      gap: 1.5rem;
    }

    .side {
      grid-template-columns: 9rem minmax(0, 1fr);
      grid-template-rows: repeat(4, max-content) 1fr;
      align-items: start;
    }

    .cover-box {
      grid-row: span 5;
    }

    .edit {
      grid-template-columns: minmax(0, 1fr);
    }
  }
</style>
