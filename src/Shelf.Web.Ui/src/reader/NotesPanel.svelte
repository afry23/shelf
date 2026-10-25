<script lang="ts">
  import type { Annotation } from '../api';
  import type { Annotations } from './annotations.svelte';
  import NoteList from './NoteList.svelte';

  // The side panel of the reader: the notes of the open file, and a note for the file itself, which needs no selection.
  let {
    store,
    fileId,
    go,
    onrecolor,
    onremove,
    selected = null,
  }: {
    store: Annotations;
    fileId: string;
    go: (annotation: Annotation) => void;
    onrecolor: (annotation: Annotation) => void;
    onremove: (annotation: Annotation) => void;
    selected?: string | null;
  } = $props();

  let adding = $state(false);
  let text = $state('');

  async function add(): Promise<void> {
    if (await store.add(fileId, { kind: 'note', note: text })) {
      adding = false;
      text = '';
    }
  }
</script>

<aside class="side" aria-label="Notes">
  <h3>Notes</h3>
  {#if adding}
    <textarea aria-label="Note" rows="4" bind:value={text}></textarea>
    <p class="actions">
      <button type="button" class="button" onclick={add}>Save note</button>
      <button type="button" class="button" onclick={() => (adding = false)}>Cancel</button>
    </p>
  {:else}
    <p class="actions"><button type="button" class="button" onclick={() => (adding = true)}>Add a note for this file</button></p>
  {/if}
  <NoteList {store} {go} {onrecolor} {onremove} {selected} />
</aside>

<style>
  .side {
    flex: 0 0 22rem;
    max-width: 45%;
    overflow: auto;
    padding: 0.75rem;
    background: var(--surface);
    border-right: 1px solid var(--line);
  }

  h3 {
    margin: 0 0 0.5rem;
  }

  textarea {
    width: 100%;
    font: inherit;
    box-sizing: border-box;
  }

  .actions {
    display: flex;
    gap: 0.4rem;
    margin: 0.25rem 0 0.75rem;
  }
</style>
