<script lang="ts" module>
  /** One line of a table of contents or an outline, whatever library it came from. */
  export interface Entry {
    title: string;
    go: () => void;
    items: Entry[];
  }
</script>

<script lang="ts">
  import Entries from './Entries.svelte';

  let { items }: { items: Entry[] } = $props();
</script>

<ul>
  {#each items as entry, index (index)}
    <li>
      <button type="button" onclick={entry.go}>{entry.title}</button>
      {#if entry.items.length}<Entries items={entry.items} />{/if}
    </li>
  {/each}
</ul>

<style>
  ul {
    list-style: none;
    margin: 0;
    padding: 0;
  }

  li > :global(ul) {
    padding-left: 1rem;
  }

  button {
    display: block;
    width: 100%;
    text-align: left;
    background: transparent;
    border: 0;
    border-radius: var(--radius);
    padding: 0.25rem 0.5rem;
    font-family: var(--serif);
    line-height: 1.35;
  }

  button:hover {
    background: var(--paper);
  }
</style>
