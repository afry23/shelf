<script lang="ts" module>
  export type Flow = 'paginated' | 'scrolled';
  export type Measure = 'narrow' | 'medium' | 'wide';
  /** The widest line of text that a measure allows, in pixels. */
  export const measures: Record<Measure, { label: string; pixels: number }> = {
    narrow: { label: 'Narrow', pixels: 560 },
    medium: { label: 'Medium', pixels: 720 },
    wide: { label: 'Wide', pixels: 960 },
  };
</script>

<script lang="ts">
  // The side panel for the layout of an EPUB: pages or a scroll, one page or two, the width of the text.
  let {
    flow = $bindable('paginated'),
    columns = $bindable(2),
    measure = $bindable('medium'),
  }: { flow?: Flow; columns?: 1 | 2; measure?: Measure } = $props();
</script>

<aside class="side" aria-label="Display">
  <h3 id="display-flow">Reading</h3>
  <div class="choice" role="group" aria-labelledby="display-flow">
    <button type="button" class="button" aria-pressed={flow === 'paginated'} onclick={() => (flow = 'paginated')}>Paged</button>
    <button type="button" class="button" aria-pressed={flow === 'scrolled'} onclick={() => (flow = 'scrolled')}>Scrolled</button>
  </div>

  <h3 id="display-pages">Pages</h3>
  <div class="choice" role="group" aria-labelledby="display-pages">
    <button type="button" class="button" disabled={flow === 'scrolled'} aria-pressed={columns === 1} onclick={() => (columns = 1)}>One page</button>
    <button type="button" class="button" disabled={flow === 'scrolled'} aria-pressed={columns === 2} onclick={() => (columns = 2)}>Two pages</button>
  </div>
  {#if columns === 2 && flow === 'paginated'}
    <p class="hint muted">A narrow window shows one page anyway.</p>
  {/if}

  <h3 id="display-width">Text width</h3>
  <div class="choice" role="group" aria-labelledby="display-width">
    {#each Object.entries(measures) as [key, m] (key)}
      <button type="button" class="button" aria-pressed={measure === key} onclick={() => (measure = key as Measure)}>{m.label}</button>
    {/each}
  </div>
</aside>

<style>
  .side {
    flex: 0 0 16rem;
    max-width: 40%;
    overflow: auto;
    padding: 1rem;
    background: var(--surface);
    border-right: 1px solid var(--line);
  }

  h3 {
    margin: 1.25rem 0 0.4rem;
    font-size: 0.9rem;
    color: var(--muted);
  }

  h3:first-child {
    margin-top: 0;
  }

  /* One joined control per choice: the buttons share the edge, the chosen one is tinted. */
  .choice {
    display: flex;
  }

  .choice .button {
    flex: 1;
    justify-content: center;
    border-radius: 0;
  }

  .choice .button + .button {
    margin-left: -1px;
  }

  .choice .button:first-child {
    border-radius: var(--radius) 0 0 var(--radius);
  }

  .choice .button:last-child {
    border-radius: 0 var(--radius) var(--radius) 0;
  }

  .choice .button[aria-pressed='true'] {
    position: relative;
    background: color-mix(in srgb, var(--accent) 16%, var(--surface));
    border-color: var(--accent);
    color: var(--accent);
    font-weight: 600;
  }

  .hint {
    margin-top: 0.4rem;
    font-size: 0.85rem;
  }
</style>
