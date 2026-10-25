<script lang="ts">
  import type { AnnotationColor } from '../api';
  import { colors } from './annotations.svelte';

  // Shown in the toolbar while a passage is selected in the book: mark it, in the color that is chosen.
  let { color = $bindable('yellow'), onhighlight }: { color?: AnnotationColor; onhighlight: () => void } = $props();
</script>

<span class="group" role="group" aria-label="Selection">
  <button type="button" class="button" onclick={onhighlight}>Highlight</button>
  {#each Object.entries(colors) as [name, value] (name)}
    <button
      type="button"
      class="swatch"
      style:background={value}
      aria-label={`Marker ${name}`}
      aria-pressed={color === name}
      onclick={() => (color = name as AnnotationColor)}
    ></button>
  {/each}
</span>

<style>
  .group {
    display: inline-flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.5rem;
  }

  .swatch {
    width: 1.1rem;
    height: 1.1rem;
    padding: 0;
    border-radius: 50%;
    border: 2px solid transparent;
  }

  .swatch[aria-pressed='true'] {
    border-color: var(--ink);
  }
</style>
