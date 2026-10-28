<script lang="ts">
  import { createWork, works, type FacetValue, type WorkList } from '../api';
  import Cover from '../components/Cover.svelte';
  import Segmented from '../components/Segmented.svelte';
  import { authorLine, plural } from '../format';
  import { addToken, describeToken, facetGroups, filterToken, hasToken, removeToken, tokens, type FacetKey } from '../query';
  import { navigate, param, setParams } from '../router.svelte';

  const sortFields = [
    { value: 'title', label: 'Title' },
    { value: 'author', label: 'Author' },
    { value: 'year', label: 'Year' },
    { value: 'added', label: 'Date added' },
    { value: 'updated', label: 'Last changed' },
  ];

  const q = $derived(param('q'));
  const sort = $derived(param('sort', 'title'));
  const order = $derived(param('order', 'asc'));
  const offset = $derived(Math.max(0, Number(param('offset', '0')) || 0));
  type View = 'grid' | 'list';
  function storedView(): View {
    try {
      return localStorage.getItem('shelf.view') === 'list' ? 'list' : 'grid';
    } catch {
      return 'grid';
    }
  }
  const view = $derived<View>(param('view') === 'list' ? 'list' : param('view') === 'grid' ? 'grid' : storedView());

  // The sizes on offer. Another size in the address is kept as it is, within what the API accepts.
  const pageSizes = [20, 40, 60, 80, 100];
  const maxPageSize = 100;
  const wantedLimit = $derived(Math.floor(Number(param('limit', ''))));
  const baseLimit = $derived(wantedLimit >= 1 ? Math.min(wantedLimit, maxPageSize) : pageSizes[0]);

  // How many covers stand in a row of the shelf, from the width of the column. This is what the grid below is told to use.
  const minCover = 152;
  const shelfGap = 24;
  let mainWidth = $state(0);
  let narrow = $state(matchMedia('(max-width: 480px)').matches);
  $effect(() => {
    const query = matchMedia('(max-width: 480px)');
    const update = () => (narrow = query.matches);
    query.addEventListener('change', update);
    return () => query.removeEventListener('change', update);
  });
  const columns = $derived(view === 'grid' ? (narrow ? 2 : Math.max(1, Math.floor((mainWidth + shelfGap) / (minCover + shelfGap)))) : 1);

  // A size from the list is rounded to whole rows, so the shelf is full on every page but the last.
  const fit = (size: number) => (pageSizes.includes(size) ? Math.max(columns, Math.min(Math.round(size / columns) * columns, Math.floor(maxPageSize / columns) * columns)) : size);
  const limit = $derived(fit(baseLimit));
  const sizeChoices = $derived(pageSizes.includes(baseLimit) ? pageSizes : [...pageSizes, baseLimit].sort((a, b) => a - b));

  let list = $state<WorkList | null>(null);
  let error = $state<string | null>(null);
  let filtersOpen = $state(false);
  let expanded = $state<Record<string, boolean>>({});

  // Fetches again whenever the address changes. An answer that comes late for an older address is dropped.
  $effect(() => {
    if (mainWidth === 0) {
      return;
    }
    const params = { q, sort, order, offset, limit };
    let current = true;
    works(params)
      .then((result) => {
        if (current) {
          list = result;
          error = null;
        }
      })
      .catch((e: Error) => {
        if (current) {
          error = e.message;
          list = null;
        }
      });
    return () => {
      current = false;
    };
  });

  const active = $derived(tokens(q));

  function toggle(field: string, value: string): void {
    const token = filterToken(field, value);
    const next = hasToken(q, token) ? removeToken(q, token) : addToken(q, token);
    setParams({ q: next, offset: null });
  }

  function chooseView(next: View): void {
    try {
      localStorage.setItem('shelf.view', next);
    } catch {
      // A convenience only.
    }
    setParams({ view: next }, { replace: true });
  }

  // A work by hand: one file, and the values that should replace what the file says.
  let creating = $state(false);
  let createBusy = $state(false);
  let createError = $state<string | null>(null);
  let createInput = $state<HTMLInputElement | null>(null);
  let createTitle = $state('');
  let createAuthor = $state('');
  let createYear = $state('');

  async function submitCreate(event: SubmitEvent): Promise<void> {
    event.preventDefault();
    const file = createInput?.files?.[0];
    if (!file) {
      createError = 'Choose a file first.';
      return;
    }
    createError = null;
    createBusy = true;
    try {
      const outcome = await createWork(file, { title: createTitle, author: createAuthor, year: createYear });
      navigate(`/works/${outcome.work}`);
    } catch (e) {
      createError = (e as Error).message;
    } finally {
      createBusy = false;
    }
  }

  const visible = (key: FacetKey, values: FacetValue[]) => (expanded[key] ? values : values.slice(0, 10));
  const last = $derived(list ? Math.min(offset + limit, list.total) : 0);
  const pageCount = $derived(list ? Math.max(1, Math.ceil(list.total / limit)) : 1);
  const page = $derived(Math.floor(offset / limit) + 1);

  // The first and the last page and the one before and after the current one; a gap between them shows as null.
  const pageItems = $derived.by(() => {
    const shown = [...new Set([1, page - 1, page, page + 1, pageCount].filter((n) => n >= 1 && n <= pageCount))].sort((a, b) => a - b);
    const items: (number | null)[] = [];
    for (const n of shown) {
      const before = items[items.length - 1];
      if (typeof before === 'number' && n - before > 1) {
        items.push(null);
      }
      items.push(n);
    }
    return items;
  });

  const goTo = (target: number) => setParams({ offset: target <= 1 ? null : String((target - 1) * limit) });
</script>

<div class="page">
  <div class="head">
    <h1>Library</h1>
    <p class="count muted" role="status">{list ? plural(list.total, 'book') : ''}</p>
    <button type="button" class="button" aria-expanded={creating} aria-controls="new-work" onclick={() => (creating = !creating)}>New work</button>
  </div>

  {#if creating}
    <form id="new-work" aria-label="New work" onsubmit={submitCreate}>
      <p class="muted">Makes a work of one file without grouping it into a work it matches. What you leave empty is read from the file.</p>
      <p><input type="file" aria-label="File of the new work" accept=".epub,.pdf,.mobi,.azw3,.cbz,.txt,.md" bind:this={createInput} /></p>
      <p><input type="text" aria-label="Title" placeholder="Title" bind:value={createTitle} /></p>
      <p><input type="text" aria-label="Author" placeholder="Author" bind:value={createAuthor} /></p>
      <p><input type="text" inputmode="numeric" aria-label="Year" placeholder="Year" bind:value={createYear} /></p>
      {#if createError}<p class="error" role="alert">{createError}</p>{/if}
      <button type="submit" class="button" disabled={createBusy}>{createBusy ? 'Uploading…' : 'Create work'}</button>
    </form>
  {/if}

  <div class="toolbar">
    <button
      type="button"
      class="button show-filters"
      aria-expanded={filtersOpen}
      aria-controls="filters"
      onclick={() => (filtersOpen = !filtersOpen)}>{filtersOpen ? 'Hide filters' : 'Show filters'}</button
    >
    <div class="sort">
      <span id="sort-label">Sort by</span>
      <select aria-labelledby="sort-label" value={sort} onchange={(e) => setParams({ sort: e.currentTarget.value === 'title' ? null : e.currentTarget.value, offset: null })}>
        {#each sortFields as field (field.value)}
          <option value={field.value}>{field.label}</option>
        {/each}
      </select>
    </div>
    <button
      type="button"
      class="button order"
      aria-label={`Sort order: ${order === 'desc' ? 'descending' : 'ascending'}`}
      onclick={() => setParams({ order: order === 'desc' ? null : 'desc', offset: null })}>{order === 'desc' ? '↓' : '↑'}</button
    >
    <Segmented
      label="View"
      options={[
        { value: 'grid', label: 'Grid' },
        { value: 'list', label: 'List' },
      ]}
      value={view}
      onchange={chooseView}
    />
  </div>

  <div class="layout">
    <aside id="filters" class="filters" class:open={filtersOpen} aria-label="Filters">
      {#if list}
        {#each facetGroups as group (group.key)}
          {@const values = list.facets[group.key]}
          {#if values.length}
            <section>
              <h3>{group.label}</h3>
              <ul>
                {#each visible(group.key, values) as item (item.value)}
                  <li>
                    <button
                      type="button"
                      aria-pressed={hasToken(q, filterToken(group.field, item.value))}
                      aria-label={`${group.label}: ${item.value} (${item.count})`}
                      onclick={() => toggle(group.field, item.value)}
                    >
                      <span class="value">{item.value}</span><span class="n">{item.count}</span>
                    </button>
                  </li>
                {/each}
              </ul>
              {#if values.length > 10}
                <button type="button" class="more" onclick={() => (expanded[group.key] = !expanded[group.key])}>
                  {expanded[group.key] ? 'Show fewer' : `Show all ${values.length}`}
                </button>
              {/if}
            </section>
          {/if}
        {/each}
      {/if}
    </aside>

    <div class="main" bind:clientWidth={mainWidth}>
      {#if active.length}
        <ul class="chips" aria-label="Active filters">
          {#each active as token (token)}
            <li>
              <button type="button" aria-label={`Remove filter ${describeToken(token)}`} onclick={() => setParams({ q: removeToken(q, token), offset: null })}>
                {describeToken(token)} <span aria-hidden="true">×</span>
              </button>
            </li>
          {/each}
        </ul>
      {/if}

      {#if error}
        <p role="alert">{error}</p>
      {:else if !list}
        <p class="muted">Loading…</p>
      {:else if list.total === 0 && !q}
        <section class="empty">
          <h2>Your library is empty</h2>
          <p>Add books with <code>shelf import &lt;folder&gt;</code>, then reload this page.</p>
        </section>
      {:else if list.total === 0}
        <section class="empty">
          <h2>No books match these filters</h2>
          <p><button type="button" class="button" onclick={() => setParams({ q: null, offset: null })}>Clear filters</button></p>
        </section>
      {:else}
        {#if view === 'grid'}
          <ul class="shelf" aria-label="Books" style:grid-template-columns={`repeat(${columns}, minmax(0, 1fr))`}>
            {#each list.items as book (book.id)}
              <li data-nav>
                <a class="book" href={`/works/${book.id}`}>
                  <span class="plank"><Cover id={book.id} title={book.title} authors={book.authors} hasCover={book.hasCover} /></span>
                  <span class="caption">
                    <span class="title">{book.title}</span>
                    {#if book.authors.length}<span class="authors">{authorLine(book.authors)}</span>{/if}
                  </span>
                </a>
              </li>
            {/each}
          </ul>
        {:else}
          <ul class="rows" aria-label="Books">
            {#each list.items as book (book.id)}
              <li data-nav>
                <a class="row" href={`/works/${book.id}`}>
                  <span class="thumb"><Cover id={book.id} title={book.title} authors={book.authors} hasCover={book.hasCover} /></span>
                  <span class="what">
                    <span class="title">{book.title}</span>
                    {#if book.subtitle}<span class="subtitle muted">{book.subtitle}</span>{/if}
                    <span class="authors">{authorLine(book.authors)}</span>
                  </span>
                  <span class="year muted">{book.year ?? ''}</span>
                  <span class="formats muted">{book.formats.join(', ')}</span>
                  <span class="state muted">{book.readStatus}</span>
                </a>
              </li>
            {/each}
          </ul>
        {/if}

        <nav class="pager" aria-label="Pages">
          <button type="button" class="button" disabled={page <= 1} onclick={() => goTo(page - 1)}>Previous page</button>
          <ol class="numbers">
            {#each pageItems as item, index (item ?? `gap${index}`)}
              <li>
                {#if item === null}
                  <span class="gap muted" aria-hidden="true">…</span>
                {:else}
                  <button type="button" class="button number" aria-label={`Page ${item}`} aria-current={item === page ? 'page' : undefined} onclick={() => goTo(item)}>{item}</button>
                {/if}
              </li>
            {/each}
          </ol>
          <button type="button" class="button" disabled={page >= pageCount} onclick={() => goTo(page + 1)}>Next page</button>
          <span class="muted">{list.total === 0 ? 0 : offset + 1}–{last} of {list.total}</span>
          <span class="muted">Page {page} of {pageCount}</span>
          <span class="size">
            <label class="muted" for="page-size">Works per page</label>
            <select id="page-size" value={String(baseLimit)} onchange={(event) => setParams({ limit: event.currentTarget.value, offset: null })}>
              {#each sizeChoices as size (size)}
                <option value={String(size)}>{fit(size)}</option>
              {/each}
            </select>
          </span>
        </nav>
      {/if}
    </div>
  </div>
</div>

<style>
  .page {
    max-width: 90rem;
    margin: 0 auto;
    padding: 1.25rem var(--gutter) 3rem;
  }

  .head {
    display: flex;
    align-items: baseline;
    gap: 1rem;
    margin-bottom: 1rem;
  }

  .toolbar {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.75rem;
    margin-bottom: 1.25rem;
  }

  .sort {
    display: inline-flex;
    align-items: center;
    gap: 0.5rem;
  }

  .show-filters {
    display: none;
  }

  .layout {
    display: grid;
    grid-template-columns: 15rem minmax(0, 1fr);
    gap: 2rem;
    align-items: start;
  }

  /* Beside the shelf the filters stay in view and scroll by themselves when a facet list is long. */
  .filters {
    position: sticky;
    top: 1rem;
    max-height: calc(100dvh - 2rem);
    overflow-y: auto;
    overscroll-behavior: contain;
    padding-right: 0.5rem;
  }

  .filters section + section {
    margin-top: 1.25rem;
  }

  .filters h3 {
    margin-bottom: 0.4rem;
  }

  .filters ul {
    list-style: none;
    margin: 0;
    padding: 0;
  }

  .filters li button {
    display: flex;
    justify-content: space-between;
    gap: 0.75rem;
    width: 100%;
    text-align: left;
    background: transparent;
    border: 0;
    border-radius: var(--radius);
    padding: 0.2rem 0.5rem;
    margin-left: -0.5rem;
  }

  .filters li button:hover {
    background: var(--surface);
  }

  .filters li button[aria-pressed='true'] {
    background: var(--accent);
    color: var(--accent-ink);
  }

  .value {
    overflow-wrap: anywhere;
  }

  .n {
    color: var(--muted);
    font-variant-numeric: tabular-nums;
  }

  button[aria-pressed='true'] .n {
    color: inherit;
    opacity: 0.8;
  }

  .more {
    background: transparent;
    border: 0;
    color: var(--accent);
    padding: 0.2rem 0;
    text-decoration: underline;
  }

  .chips {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
    list-style: none;
    margin: 0 0 1.25rem;
    padding: 0;
  }

  .chips button {
    border: 1px solid var(--accent);
    color: var(--accent);
    background: transparent;
    border-radius: 999px;
    padding: 0.15rem 0.75rem;
  }

  .empty {
    padding: 3rem 0;
    max-width: 32rem;
  }

  .empty h2 {
    margin-bottom: 0.5rem;
  }

  /* The shelf: every cover stands on a plank, and the planks of one row join up. */
  .shelf {
    --gap: 1.5rem;
    list-style: none;
    margin: 0;
    padding: 0;
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(9.5rem, 1fr));
    column-gap: var(--gap);
    row-gap: 1.75rem;
  }

  .book {
    display: block;
    color: inherit;
    text-decoration: none;
  }

  .plank {
    position: relative;
    display: block;
    padding-bottom: 0.55rem;
  }

  .plank::after {
    content: '';
    position: absolute;
    left: calc(var(--gap) / -2);
    right: calc(var(--gap) / -2);
    bottom: 0;
    height: 0.55rem;
    background: linear-gradient(var(--shelf), var(--shelf-edge));
    box-shadow: 0 3px 5px -2px rgb(0 0 0 / 0.35);
  }

  .caption {
    display: block;
    margin-top: 0.6rem;
  }

  .title {
    display: block;
    font-family: var(--serif);
    font-weight: 600;
    line-height: 1.25;
  }

  .book:hover .title,
  .row:hover .title {
    text-decoration: underline;
    text-decoration-color: var(--accent);
  }

  .authors {
    display: block;
    color: var(--muted);
    font-size: 0.875rem;
  }

  .rows {
    list-style: none;
    margin: 0;
    padding: 0;
    border-top: 1px solid var(--line);
  }

  .rows li {
    border-bottom: 1px solid var(--line);
  }

  .row {
    display: grid;
    grid-template-columns: 3rem minmax(0, 1fr) 4rem 8rem 5rem;
    gap: 1rem;
    align-items: center;
    padding: 0.6rem 0.25rem;
    color: inherit;
    text-decoration: none;
  }

  .row:hover {
    background: var(--surface);
  }

  .thumb :global(.cover) {
    font-size: 0.4rem;
  }

  .thumb :global(.spine) {
    padding: 0.2rem;
  }

  .thumb :global(.spine .title),
  .thumb :global(.spine .author) {
    display: none;
  }

  .subtitle {
    display: block;
    font-size: 0.875rem;
  }

  .pager {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    justify-content: center;
    gap: 0.75rem 1rem;
    margin-top: 2rem;
  }

  .numbers {
    display: flex;
    align-items: center;
    gap: 0.25rem;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .number[aria-current='page'] {
    font-weight: 700;
    border-color: currentColor;
  }

  .gap {
    padding: 0 0.25rem;
  }

  .size {
    display: flex;
    align-items: center;
    gap: 0.5rem;
  }

  /* Tablet and narrower: the filters sit behind a button. */
  @media (max-width: 959px) {
    .layout {
      grid-template-columns: minmax(0, 1fr);
      gap: 1rem;
    }

    .show-filters {
      display: inline-flex;
    }

    .filters {
      display: none;
      position: static;
      max-height: min(70dvh, 36rem);
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--radius);
      padding: 1rem;
    }

    /* The facets stand in as many columns as the panel has room for, so that the panel does not push the shelf off the screen. */
    .filters.open {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr));
      gap: 1rem 1.5rem;
      align-items: start;
    }

    .filters section + section {
      margin-top: 0;
    }

    .row {
      grid-template-columns: 3rem minmax(0, 1fr) 4rem;
    }

    .row .formats,
    .row .state {
      display: none;
    }
  }

  @media (max-width: 480px) {
    .shelf {
      --gap: 1.25rem;
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }

    .toolbar {
      gap: 0.6rem;
    }

    .sort {
      flex: 1 1 10rem;
    }

    .sort select {
      flex: 1;
      min-width: 0;
    }

    /* Previous and next share the first row; the page numbers get a row of their own and scroll inside it. */
    .pager > .button {
      flex: 1 1 40%;
      justify-content: center;
    }

    .numbers {
      order: 3;
      flex-basis: 100%;
      justify-content: safe center;
      overflow-x: auto;
      padding-bottom: 0.25rem;
    }

    .numbers .number {
      min-width: 2.5rem;
    }

    .row {
      gap: 0.75rem;
    }

    .row .year {
      display: none;
    }

    .row {
      grid-template-columns: 3rem minmax(0, 1fr);
    }
  }

  /* A finger needs more room than a pointer. */
  @media (pointer: coarse) {
    .filters li button,
    .more,
    .chips button {
      min-height: 2.5rem;
      align-items: center;
    }

    .filters li button {
      padding-inline: 0.6rem;
    }
  }
</style>
