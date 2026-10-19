<script lang="ts">
  import { search, type SearchLevel, type SearchMode, type SearchResponse } from '../api';
  import Segmented from '../components/Segmented.svelte';
  import { authorLine, plural, readLink, snippetParts } from '../format';
  import { param, setParams } from '../router.svelte';

  const q = $derived(param('q').trim());
  const mode = $derived<SearchMode>((['semantic', 'hybrid'] as const).find((m) => m === param('mode')) ?? 'fulltext');
  const level = $derived<SearchLevel>(param('level') === 'chunk' ? 'chunk' : 'work');

  let result = $state<SearchResponse | null>(null);
  let error = $state<string | null>(null);
  let waiting = $state(false);

  $effect(() => {
    const params = { q, mode, level: mode === 'fulltext' ? undefined : level };
    if (!params.q) {
      result = null;
      error = null;
      return;
    }
    let current = true;
    waiting = true;
    search(params)
      .then((r) => {
        if (current) {
          result = r;
          error = null;
        }
      })
      .catch((e: Error) => {
        if (current) {
          result = null;
          error = e.message;
        }
      })
      .finally(() => {
        if (current) {
          waiting = false;
        }
      });
    return () => {
      current = false;
    };
  });
</script>

<div class="page">
  <h1>Search</h1>

  <div class="controls">
    <Segmented
      label="Search mode"
      options={[
        { value: 'fulltext', label: 'Full text' },
        { value: 'semantic', label: 'Meaning' },
        { value: 'hybrid', label: 'Both' },
      ]}
      value={mode}
      onchange={(m) => setParams(m === 'fulltext' ? { mode: null, level: null } : { mode: m })}
    />
    {#if mode !== 'fulltext'}
      <Segmented
        label="Search level"
        options={[
          { value: 'work', label: 'Books' },
          { value: 'chunk', label: 'Passages' },
        ]}
        value={level}
        onchange={(l) => setParams({ level: l === 'work' ? null : l })}
      />
    {/if}
  </div>

  {#if !q}
    <section class="empty">
      <p>Type a word or a phrase in the search field.</p>
      <p class="muted">Filters work too, for example <code>tag:cloud year:&gt;=2015</code>.</p>
    </section>
  {:else if error}
    <p role="alert">{error}</p>
  {:else if result}
    {#if result.note}<p class="note" role="status">{result.note}</p>{/if}
    {#if result.total === 0}
      <section class="empty"><h2>Nothing found for {q}</h2></section>
    {:else}
      <p class="count muted" role="status">{plural(result.total, 'book')} for “{q}”</p>
      <ul class="results" aria-label="Results">
        {#each result.results as item (item.id)}
          <li data-nav>
            <h2><a href={`/works/${item.id}`}>{item.title}</a></h2>
            <p class="byline">
              <span>{authorLine(item.authors)}</span>
              {#if item.year}<span class="muted">{item.year}</span>{/if}
              <span class="muted">{item.formats.join(', ')}</span>
            </p>
            {#if item.passages.length}
              <ul class="passages">
                {#each item.passages as passage, index (index)}
                  <li>
                    <a href={readLink(passage.fileId, passage.location)} aria-label={`Open at ${passage.where}`}>
                      <span class="where">{passage.where}</span>
                      <span class="snippet">
                        {#each snippetParts(passage.snippet) as part, i (i)}{#if part.hit}<mark>{part.text}</mark>{:else}{part.text}{/if}{/each}
                      </span>
                    </a>
                  </li>
                {/each}
              </ul>
            {/if}
          </li>
        {/each}
      </ul>
    {/if}
  {:else if waiting}
    <p class="muted">Searching…</p>
  {/if}
</div>

<style>
  .page {
    max-width: 52rem;
    margin: 0 auto;
    padding: 1.25rem var(--gutter) 4rem;
  }

  .controls {
    display: flex;
    flex-wrap: wrap;
    gap: 0.75rem;
    margin: 1rem 0 1.5rem;
  }

  .empty {
    padding: 2rem 0;
  }

  .note {
    border-left: 3px solid var(--accent);
    background: var(--surface);
    padding: 0.6rem 0.85rem;
    margin-bottom: 1rem;
  }

  .count {
    margin-bottom: 1rem;
  }

  .results,
  .passages {
    list-style: none;
    margin: 0;
    padding: 0;
  }

  .results > li {
    padding: 1.1rem 0;
    border-top: 1px solid var(--line);
  }

  .results h2 a {
    color: var(--ink);
    text-decoration-color: var(--accent);
  }

  .byline {
    display: flex;
    flex-wrap: wrap;
    gap: 0.25rem 1rem;
    font-size: 0.9rem;
    margin-top: 0.15rem;
  }

  .passages {
    margin-top: 0.6rem;
    display: grid;
    gap: 0.4rem;
  }

  .passages a {
    display: grid;
    grid-template-columns: 6.5rem minmax(0, 1fr);
    gap: 0.75rem;
    color: inherit;
    text-decoration: none;
    padding: 0.4rem 0.5rem;
    margin: 0 -0.5rem;
    border-radius: var(--radius);
  }

  .passages a:hover {
    background: var(--surface);
  }

  .where {
    color: var(--accent);
    font-size: 0.875rem;
  }

  .snippet {
    font-family: var(--serif);
    line-height: 1.55;
    max-width: 40rem;
  }

  @media (max-width: 560px) {
    .passages a {
      grid-template-columns: minmax(0, 1fr);
      gap: 0.1rem;
    }
  }
</style>
