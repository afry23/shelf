<script lang="ts">
  import { ApiError, work as loadWork, type WorkDetail, type WorkFile } from '../api';
  import { FileIds } from '../fileids';

  // The route /read/{fileId}: finds the file among the files of its work and opens the reader for its format. The libraries are
  // loaded when a book is opened, not with the rest of the interface.
  let { fileId }: { fileId: string } = $props();

  let book = $state<WorkDetail | null>(null);
  let file = $state<WorkFile | null>(null);
  let phase = $state<'loading' | 'ready' | 'missing' | 'error'>('loading');
  let message = $state('');

  $effect(() => {
    const parts = FileIds.split(fileId);
    let current = true;
    phase = 'loading';
    book = null;
    file = null;
    if (!parts) {
      phase = 'missing';
      return;
    }
    loadWork(parts.work)
      .then((detail) => {
        if (!current) {
          return;
        }
        const found = detail.files.find((f) => f.id === fileId) ?? null;
        book = detail;
        file = found;
        phase = found ? 'ready' : 'missing';
      })
      .catch((e: Error) => {
        if (current) {
          phase = e instanceof ApiError && e.status === 404 ? 'missing' : 'error';
          message = e.message;
        }
      });
    return () => {
      current = false;
    };
  });
</script>

{#if phase === 'ready' && book && file}
  {#if file.format === 'pdf'}
    {#await import('../reader/PdfReader.svelte') then { default: PdfReader }}
      <PdfReader {book} {file} />
    {:catch}
      <p class="notice" role="alert">The reader could not be loaded.</p>
    {/await}
  {:else if file.format === 'epub'}
    {#await import('../reader/EpubReader.svelte') then { default: EpubReader }}
      <EpubReader {book} {file} />
    {:catch}
      <p class="notice" role="alert">The reader could not be loaded.</p>
    {/await}
  {:else if file.format === 'md'}
    {#await import('../reader/MarkdownReader.svelte') then { default: MarkdownReader }}
      <MarkdownReader {book} {file} />
    {:catch}
      <p class="notice" role="alert">The reader could not be loaded.</p>
    {/await}
  {:else}
    <section class="notice">
      <h1>This format has no reader</h1>
      <p><a href={`/works/${book.id}`}>Back to the book</a></p>
    </section>
  {/if}
{:else if phase === 'missing'}
  <section class="notice">
    <h1>This file is not in the library</h1>
    <p><a href="/library">Back to the library</a></p>
  </section>
{:else if phase === 'error'}
  <p class="notice" role="alert">{message}</p>
{:else}
  <p class="notice muted">Opening…</p>
{/if}

<style>
  .notice {
    max-width: 40rem;
    margin: 0 auto;
    padding: 3rem var(--gutter);
  }

  .notice h1 {
    margin-bottom: 0.75rem;
  }
</style>
