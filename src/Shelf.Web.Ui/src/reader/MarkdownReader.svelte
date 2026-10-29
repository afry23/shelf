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
  .scroll {
    position: absolute;
    inset: 0;
    overflow: auto;
    background: var(--bg, #fbfaf7);
  }

  .article {
    max-width: 42rem;
    margin: 0 auto;
    padding: 2rem var(--gutter) 4rem;
    font-family: var(--serif);
    font-size: 1.125rem;
    line-height: 1.65;
  }

  .article :global(img) {
    display: block;
    max-width: 100%;
    height: auto;
    margin: 1.25rem auto;
  }

  .article :global(pre) {
    overflow: auto;
    padding: 0.75rem 1rem;
    background: var(--line);
    border-radius: 4px;
  }

  .article :global(blockquote) {
    margin-left: 0;
    padding-left: 1rem;
    border-left: 3px solid var(--line);
  }

  .notice {
    padding: 3rem var(--gutter);
  }
</style>
