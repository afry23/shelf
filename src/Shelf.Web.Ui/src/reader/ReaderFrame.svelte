<script lang="ts">
  import type { Snippet } from 'svelte';
  import type { WorkDetail } from '../api';

  // The frame around both readers: the way back, the title, the controls of the format, an optional side panel, the surface.
  let {
    book,
    toolbar,
    side,
    children,
    background,
  }: { book: WorkDetail; toolbar: Snippet; side?: Snippet; children: Snippet; background?: string } = $props();
</script>

<div class="reader" style:--reader-bg={background}>
  <header class="bar" role="toolbar" aria-label="Reader">
    <a class="back" href={`/works/${book.id}`}>Back to the book</a>
    <span class="title">{book.title}</span>
    <div class="controls">{@render toolbar()}</div>
  </header>
  <div class="body">
    {#if side}{@render side()}{/if}
    <div class="surface">{@render children()}</div>
  </div>
</div>

<style>
  .reader {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
  }

  .bar {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.5rem 1rem;
    padding: 0.5rem var(--gutter);
    border-bottom: 1px solid var(--line);
    background: var(--surface);
  }

  .back {
    white-space: nowrap;
  }

  .title {
    font-family: var(--serif);
    font-weight: 600;
    min-width: 0;
    flex: 1 1 8rem;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .controls {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.5rem 0.75rem;
  }

  .body {
    flex: 1;
    min-height: 0;
    display: flex;
  }

  .surface {
    flex: 1;
    min-width: 0;
    position: relative;
    background: var(--reader-bg, var(--line));
  }
</style>
