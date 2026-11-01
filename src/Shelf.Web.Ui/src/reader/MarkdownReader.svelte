<script lang="ts">
  import { fileUrl, type WorkDetail, type WorkFile } from '../api';
  import { renderArticle } from '../markdown';
  import ReaderFrame from './ReaderFrame.svelte';

  // The reader of an article (F-ART-8): the Markdown of the file, with the images that were saved next to it.
  let { book, file }: { book: WorkDetail; file: WorkFile } = $props();

  let html = $state('');
  let error = $state<string | null>(null);
  let loading = $state(true);

  $effect(() => {
    let current = true;
    loading = true;
    error = null;
    fetch(fileUrl(file.id))
      .then((response) => {
        if (!response.ok) {
          throw new Error(`The article could not be loaded (${response.status}).`);
        }
        return response.text();
      })
      .then((text) => {
        if (current) {
          html = renderArticle(text, file.id);
        }
      })
      .catch((e: Error) => {
        if (current) {
          error = e.message;
        }
      })
      .finally(() => {
        if (current) {
          loading = false;
        }
      });
    return () => {
      current = false;
    };
  });
</script>

<ReaderFrame {book}>
  {#snippet toolbar()}{/snippet}
  <div class="scroll">
    {#if error}
      <p class="notice" role="alert">{error}</p>
    {:else if loading}
      <p class="notice muted">Opening…</p>
    {:else}
      <article class="article" aria-label={book.title}>{@html html}</article>
    {/if}
  </div>
</ReaderFrame>

<style>
  /* The article sits on the paper of the interface (so it follows the dark mode), in a column of about 65 characters. */
  .scroll {
    position: absolute;
    inset: 0;
    overflow: auto;
    background: var(--surface);
  }

  .article {
    max-width: 40rem;
    margin: 0 auto;
    padding: 2.5rem var(--gutter) 5rem;
    font-family: var(--serif);
    font-size: 1.1875rem;
    line-height: 1.7;
    hanging-punctuation: first last;
  }

  .article :global(h1),
  .article :global(h2),
  .article :global(h3) {
    margin: 2rem 0 0.75rem;
    line-height: 1.25;
  }

  .article :global(h1:first-child) {
    margin-top: 0;
  }

  .article :global(p),
  .article :global(ul),
  .article :global(ol) {
    margin: 0 0 1.1em;
  }

  .article :global(img) {
    display: block;
    max-width: 100%;
    height: auto;
    margin: 1.5rem auto;
    border-radius: var(--radius);
  }

  .article :global(pre) {
    overflow: auto;
    padding: 0.75rem 1rem;
    background: var(--paper);
    border: 1px solid var(--line);
    border-radius: var(--radius);
    font-size: 0.9rem;
    line-height: 1.5;
  }

  .article :global(pre code) {
    border: 0;
    padding: 0;
    background: none;
  }

  .article :global(blockquote) {
    margin: 0 0 1.1em;
    padding-left: 1.1rem;
    border-left: 3px solid var(--accent);
    color: var(--muted);
    font-style: italic;
  }

  .notice {
    padding: 3rem var(--gutter);
  }
</style>
