<script lang="ts">
  import type { Annotation, AnnotationColor } from '../api';
  import { placeName } from '../format';
  import { renderMarkdown } from '../markdown';
  import type { Annotations } from './annotations.svelte';
  import { colors } from './annotations.svelte';

  // The highlights, notes and bookmarks of a file or a work, with the note shown as Markdown and an editor for it. The reader goes to the place
  // of a highlight (`go`); the book page links to it (`href`).
  let {
    store,
    go,
    href,
    onrecolor,
    onremove,
    selected = null,
    names = {},
  }: {
    store: Annotations;
    go?: (annotation: Annotation) => void;
    href?: (annotation: Annotation) => string;
    onrecolor?: (annotation: Annotation) => void;
    onremove?: (annotation: Annotation) => void;
    selected?: string | null;
    /** File names by file ID: shown when the list covers more than one file. */
    names?: Record<string, string>;
  } = $props();

  let editing = $state<string | null>(null);
  let draft = $state('');

  function edit(annotation: Annotation): void {
    editing = annotation.id;
    draft = annotation.note ?? '';
  }

  async function save(annotation: Annotation): Promise<void> {
    if (await store.changeNote(annotation, draft.trim() ? draft : null)) {
      editing = null;
    }
  }

  async function recolor(annotation: Annotation, color: AnnotationColor): Promise<void> {
    const updated = await store.changeColor(annotation, color);
    if (updated) {
      onrecolor?.(updated);
    }
  }

  async function remove(annotation: Annotation): Promise<void> {
    if (await store.remove(annotation)) {
      onremove?.(annotation);
    }
  }
</script>

{#if store.items.length === 0}
  <p class="muted empty">{store.loaded ? 'No highlights, notes or bookmarks yet.' : 'Loading…'}</p>
{:else}
  <ul class="notes">
    {#each store.items as annotation (annotation.id)}
      <li class:selected={selected === annotation.id}>
        {#if annotation.quote}
          {#if href}
            <a class="quote" style:border-color={colors[annotation.color ?? 'yellow']} href={href(annotation)}>{annotation.quote.exact}</a>
          {:else}
            <button type="button" class="quote" style:border-color={colors[annotation.color ?? 'yellow']} onclick={() => go?.(annotation)}>
              {annotation.quote.exact}
            </button>
          {/if}
        {:else if annotation.kind === 'bookmark'}
          {#if href}
            <a class="quote bookmark" href={href(annotation)}>{placeName(annotation.locator)}</a>
          {:else}
            <button type="button" class="quote bookmark" onclick={() => go?.(annotation)}>{placeName(annotation.locator)}</button>
          {/if}
        {:else}
          <p class="kind muted">Note for the file{names[annotation.file] ? ` ${names[annotation.file]}` : ''}</p>
        {/if}

        {#if editing === annotation.id}
          <textarea aria-label="Note" rows="4" bind:value={draft}></textarea>
          <p class="actions">
            <button type="button" class="button" onclick={() => save(annotation)}>Save note</button>
            <button type="button" class="button" onclick={() => (editing = null)}>Cancel</button>
          </p>
        {:else}
          {#if annotation.note}
            <div class="markdown">{@html renderMarkdown(annotation.note)}</div>
          {/if}
          <p class="actions">
            <button type="button" class="button" onclick={() => edit(annotation)}>{annotation.kind === 'bookmark' ? (annotation.note ? 'Edit label' : 'Add label') : annotation.note ? 'Edit note' : 'Add note'}</button>
            {#if annotation.kind === 'highlight' && annotation.color}
              <span class="swatches" role="group" aria-label="Color">
                {#each Object.entries(colors) as [name, value] (name)}
                  <button
                    type="button"
                    class="swatch"
                    style:background={value}
                    aria-label={`Color ${name}`}
                    aria-pressed={annotation.color === name}
                    onclick={() => recolor(annotation, name as AnnotationColor)}
                  ></button>
                {/each}
              </span>
            {/if}
            <button type="button" class="button" onclick={() => remove(annotation)}>
              {annotation.kind === 'highlight' ? 'Delete highlight' : annotation.kind === 'bookmark' ? 'Delete bookmark' : 'Delete note'}
            </button>
          </p>
        {/if}
      </li>
    {/each}
  </ul>
{/if}

{#if store.error}
  <p class="problem" role="alert">{store.error}</p>
{/if}

<style>
  .notes {
    list-style: none;
    margin: 0;
    padding: 0;
    display: grid;
    gap: 0.75rem;
  }

  li {
    padding: 0.5rem;
    border: 1px solid var(--line);
    border-radius: var(--radius);
    background: var(--paper);
  }

  li.selected {
    outline: 2px solid var(--accent);
  }

  .quote {
    display: block;
    width: 100%;
    text-align: left;
    background: transparent;
    border: 0;
    border-left: 4px solid;
    padding: 0.1rem 0.5rem;
    font-family: var(--serif);
    font-style: italic;
    color: inherit;
    text-decoration: none;
  }

  .bookmark {
    font-style: normal;
    border-color: var(--accent);
  }

  .kind {
    margin: 0;
  }

  .markdown {
    margin: 0.5rem 0;
    overflow-wrap: anywhere;
  }

  .markdown :global(p) {
    margin: 0.25rem 0;
  }

  textarea {
    width: 100%;
    margin-top: 0.5rem;
    font: inherit;
    box-sizing: border-box;
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.4rem;
    margin: 0.5rem 0 0;
  }

  .swatches {
    display: inline-flex;
    gap: 0.25rem;
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

  .empty {
    margin: 0;
  }

  .problem {
    color: var(--danger, #b3261e);
  }
</style>
