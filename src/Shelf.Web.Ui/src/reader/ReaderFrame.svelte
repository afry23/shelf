<script lang="ts">
  import type { Snippet } from 'svelte';
  import type { WorkDetail } from '../api';

  // The frame around both readers: the way back and the title on top, below them the tools of the format in groups, an optional
  // side panel, the surface.
  let {
    book,
    toolbar,
    side,
    children,
    background,
  }: { book: WorkDetail; toolbar: Snippet; side?: Snippet; children: Snippet; background?: string } = $props();
</script>

<div class="reader" style:--reader-bg={background}>
  <header class="bar">
    <div class="head">
      <a class="back" href={`/works/${book.id}`}>Back to the book</a>
      <span class="title">{book.title}</span>
    </div>
    <div class="controls" role="toolbar" aria-label="Reader">{@render toolbar()}</div>
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
    background: var(--surface);
    border-bottom: 1px solid var(--line);
  }

  /* Top tier: where the person is. Quiet, so the book is the loud thing. */
  .head {
    display: flex;
    align-items: baseline;
    gap: 1rem;
    padding: 0.5rem var(--gutter) 0.15rem;
  }

  .back {
    flex: none;
    font-size: 0.875rem;
    color: var(--muted);
    text-decoration: none;
    white-space: nowrap;
  }

  .back::before {
    content: '\2190';
    margin-right: 0.35em;
  }

  .back:hover {
    color: var(--accent);
    text-decoration: underline;
  }

  .title {
    flex: 1;
    min-width: 0;
    font-family: var(--serif);
    font-size: 1.05rem;
    font-weight: 600;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  /* Second tier: the tools, in groups that are told apart by a hairline, not by boxes around every button. */
  .controls {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.35rem 0;
    padding: 0.25rem var(--gutter) 0.5rem;
    font-size: 0.9rem;
  }

  .controls > :global(*) {
    margin-right: 0.25rem;
  }

  .controls :global(.group) {
    display: inline-flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.25rem;
    padding-left: 0.75rem;
    margin-left: 0.5rem;
    border-left: 1px solid var(--line);
  }

  .controls :global(.group:first-child),
  .controls > :global(:first-child) {
    border-left: 0;
    padding-left: 0;
    margin-left: 0;
  }

  /* Tool buttons are flat: the border shows only when the pointer or the focus is there, the state shows as a tint. */
  .controls :global(.button) {
    border-color: transparent;
    background: transparent;
    padding: 0.25rem 0.6rem;
    font-size: inherit;
  }

  .controls :global(.button:hover:not([disabled])) {
    background: var(--paper);
    border-color: var(--line);
  }

  .controls :global(.button[aria-pressed='true']),
  .controls :global(.button[aria-expanded='true']) {
    background: color-mix(in srgb, var(--accent) 16%, transparent);
    border-color: color-mix(in srgb, var(--accent) 45%, transparent);
    color: var(--accent);
    font-weight: 600;
  }

  /* Icon buttons: square, so a row of them keeps an even rhythm. */
  .controls :global(.button.icon) {
    justify-content: center;
    min-width: 2rem;
    height: 2rem;
    padding: 0;
  }

  .controls :global(.button.glyph) {
    font-family: var(--serif);
    font-size: 1.05rem;
    font-weight: 600;
  }

  .controls :global(.button.glyph sup) {
    font-size: 0.65em;
    margin-left: 0.05em;
  }

  .controls :global(input) {
    padding: 0.25rem 0.6rem;
    font-size: inherit;
  }

  .controls :global(input[type='search']) {
    width: clamp(9rem, 16vw, 14rem);
  }

  .controls :global(.status),
  .controls :global(.muted) {
    font-variant-numeric: tabular-nums;
    color: var(--muted);
    white-space: nowrap;
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

  @media (max-width: 40rem) {
    .controls {
      flex-wrap: nowrap;
      overflow-x: auto;
    }

    .controls :global(.group) {
      flex-wrap: nowrap;
    }

    .controls :global(.button),
    .controls :global(.status),
    .controls :global(.muted) {
      white-space: nowrap;
    }
  }
</style>
