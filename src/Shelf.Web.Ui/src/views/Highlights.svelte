<script lang="ts">
  import { exportAddress, resurface, type Resurfaced } from '../api';
  import { authorLine, readLink } from '../format';
  import { renderMarkdown } from '../markdown';

  // F-ANN-10 and F-ANN-9: a few highlights that come back today, and the export of all highlights and notes. Which highlights
  // come is decided by the server (the same day gives the same ones); this view only lays them out.
  let result = $state<Resurfaced | null>(null);
  let error = $state<string | null>(null);

  $effect(() => {
    resurface()
      .then((r) => (result = r))
      .catch((e: Error) => (error = e.message));
  });
</script>

<div class="page">
  <h1>Highlights</h1>

  {#if error}<p role="alert">{error}</p>{/if}

  {#if result}
    {#if result.highlights.length === 0}
      <section class="empty">
        <h2>No highlights yet</h2>
        <p class="muted">Select text in the reader and choose a color. A few of your highlights come back here each day.</p>
      </section>
    {:else}
      <p class="muted intro">From your reading, for {result.date}.</p>
      <ul class="highlights" aria-label="Highlights">
        {#each result.highlights as item (item.id)}
          <li>
            <figure data-nav>
              <blockquote data-color={item.color}>{item.quote}</blockquote>
              {#if item.note}<div class="note">{@html renderMarkdown(item.note)}</div>{/if}
              <figcaption>
                <a href={readLink(item.file, item.locator)}>{item.title}{item.place ? `, ${item.place[0].toUpperCase()}${item.place.slice(1)}` : ''}</a>
                {#if item.authors.length}<span class="muted"> · {authorLine(item.authors)}</span>{/if}
              </figcaption>
            </figure>
          </li>
        {/each}
      </ul>
    {/if}
  {:else if !error}
    <p class="muted">Loading…</p>
  {/if}

  <section class="export">
    <h2>Export</h2>
    <p class="muted">All highlights and notes of the library, to take to other tools.</p>
    <p>
      <a href={exportAddress('markdown')} download>Markdown</a>
      <span class="muted"> (works with a cite key, for example for Obsidian)</span>
    </p>
    <p>
      <a href={exportAddress('readwise-csv')} download>Readwise CSV</a>
      <span class="muted"> (for the Readwise import)</span>
    </p>
  </section>
</div>

<style>
  .page {
    max-width: 44rem;
    margin: 0 auto;
    padding: 1.25rem var(--gutter) 4rem;
  }

  .intro {
    margin: 0.5rem 0 1.25rem;
  }

  ul {
    list-style: none;
    margin: 0;
    padding: 0;
  }

  .highlights {
    display: grid;
    gap: 1rem;
  }

  figure {
    margin: 0;
    padding: 1rem;
    background: var(--surface);
    border: 1px solid var(--line);
    border-radius: var(--radius);
    display: grid;
    gap: 0.6rem;
  }

  blockquote {
    margin: 0;
    padding-left: 0.9rem;
    border-left: 4px solid var(--line);
    font-family: var(--serif);
    font-size: 1.1rem;
  }

  blockquote[data-color='yellow'] {
    border-left-color: #e6c84c;
  }

  blockquote[data-color='green'] {
    border-left-color: #5fb36b;
  }

  blockquote[data-color='blue'] {
    border-left-color: #5b9bd5;
  }

  blockquote[data-color='pink'] {
    border-left-color: #e07aa6;
  }

  blockquote[data-color='orange'] {
    border-left-color: #e8964a;
  }

  .note :global(p) {
    margin: 0;
  }

  .export {
    margin-top: 2.5rem;
    padding-top: 1rem;
    border-top: 1px solid var(--line);
  }

  .export p {
    margin: 0.4rem 0;
  }
</style>
