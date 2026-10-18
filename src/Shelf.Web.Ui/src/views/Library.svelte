<script lang="ts">
  import { works, type FacetValue, type WorkList } from '../api';
  import Cover from '../components/Cover.svelte';
  import Segmented from '../components/Segmented.svelte';
  import { authorLine, plural } from '../format';
  import { addToken, describeToken, facetGroups, filterToken, hasToken, removeToken, tokens, type FacetKey } from '../query';
  import { param, setParams } from '../router.svelte';

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
  const limit = $derived(Math.max(1, Number(param('limit', '48')) || 48));

  type View = 'grid' | 'list';
  function storedView(): View {
    try {
      return localStorage.getItem('shelf.view') === 'list' ? 'list' : 'grid';
    } catch {
      return 'grid';
    }
  }
  const view = $derived<View>(param('view') === 'list' ? 'list' : param('view') === 'grid' ? 'grid' : storedView());

  let list = $state<WorkList | null>(null);
  let error = $state<string | null>(null);
  let filtersOpen = $state(false);
  let expanded = $state<Record<string, boolean>>({});

  // Fetches again whenever the address changes. An answer that comes late for an older address is dropped.
  $effect(() => {
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

  const visible = (key: FacetKey, values: FacetValue[]) => (expanded[key] ? values : values.slice(0, 10));
  const last = $derived(list ? Math.min(offset + limit, list.total) : 0);
</script>

<div class="page">
  <div class="head">
    <h1>Library</h1>
    <p class="count muted" role="status">{list ? plural(list.total, 'book') : ''}</p>
  </div>

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

    <div class="main">
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
          <ul class="shelf" aria-label="Books">
            {#each list.items as book (book.id)}
              <li>
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
              <li>
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
          <button type="button" class="button" disabled={offset === 0} onclick={() => setParams({ offset: String(Math.max(0, offset - limit)) })}>Previous page</button>
          <span class="muted">{offset + 1}–{last} of {list.total}</span>
          <button type="button" class="button" disabled={last >= list.total} onclick={() => setParams({ offset: String(offset + limit) })}>Next page</button>
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
    align-items: center;
    justify-content: center;
    gap: 1rem;
    margin-top: 2rem;
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
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--radius);
      padding: 1rem;
    }

    .filters.open {
      display: block;
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
  }
</style>
