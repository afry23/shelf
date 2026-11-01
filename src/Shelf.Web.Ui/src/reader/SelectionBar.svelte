<script lang="ts">
  import type { AnnotationColor } from '../api';
  import { colors } from './annotations.svelte';
  import Icon from './Icon.svelte';

  // Always in the toolbar, so the layout does not move when a passage is selected; the buttons work while one is (active). Mark it, in the color that is chosen, or copy it as a quote (needs a cite key).
  let {
    color = $bindable('yellow'),
    onhighlight,
    oncite,
    citable = true,
    active = false,
  }: { color?: AnnotationColor; onhighlight: () => void; oncite: () => void; citable?: boolean; active?: boolean } = $props();
</script>

<span class="group" role="group" aria-label="Selection">
  <button type="button" class="button icon" disabled={!active} title={active ? 'Highlight' : 'Highlight (select a passage first)'} aria-label="Highlight" onclick={onhighlight}>
    <Icon name="highlight" />
  </button>
  <button
    type="button"
    class="button icon"
    disabled={!active || !citable}
    aria-label="Copy as quote"
    title={!citable ? 'Copy as quote (set a cite key on the book page first)' : active ? 'Copy as quote: the passage with its cite key and place' : 'Copy as quote (select a passage first)'}
    onclick={oncite}><Icon name="quote" /></button
  >
  {#each Object.entries(colors) as [name, value] (name)}
    <button
      type="button"
      class="swatch"
      style:background={value}
      aria-label={`Marker ${name}`}
      title={`Marker ${name}`}
      aria-pressed={color === name}
      onclick={() => (color = name as AnnotationColor)}
    ></button>
  {/each}
</span>

<style>
  .swatch {
    width: 1.1rem;
    height: 1.1rem;
    padding: 0;
    border-radius: 50%;
    border: 1px solid color-mix(in srgb, var(--ink) 25%, transparent);
    transition: transform 0.12s;
  }

  .swatch:hover {
    transform: scale(1.15);
  }

  .swatch[aria-pressed='true'] {
    box-shadow:
      0 0 0 2px var(--surface),
      0 0 0 4px var(--ink);
  }
</style>
