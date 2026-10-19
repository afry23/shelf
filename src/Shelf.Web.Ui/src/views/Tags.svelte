<script lang="ts">
  import { mergeTag, renameTag, tags as loadTags, type TagNode, type TagTree } from '../api';
  import { plural } from '../format';

  let tree = $state<TagTree | null>(null);
  let error = $state<string | null>(null);
  let message = $state<string | null>(null);
  // The tag that is being renamed or merged, with the text of the form.
  let editing = $state<{ path: string; action: 'rename' | 'merge'; text: string } | null>(null);
  let busy = $state(false);

  async function load(): Promise<void> {
    try {
      tree = await loadTags();
    } catch (e) {
      error = (e as Error).message;
    }
  }

  $effect(() => {
    void load();
  });

  const start = (path: string, action: 'rename' | 'merge') => (editing = { path, action, text: action === 'rename' ? path : '' });

  async function submit(event: SubmitEvent): Promise<void> {
    event.preventDefault();
    if (!editing || busy) {
      return;
    }
    const { path, action, text } = editing;
    busy = true;
    try {
      const done = action === 'rename' ? await renameTag(path, text.trim()) : await mergeTag(path, text.trim());
      message =
        action === 'rename'
          ? `Renamed ${done.from} to ${done.to} on ${plural(done.changed, 'work')}.`
          : `Merged ${done.from} into ${done.to} on ${plural(done.changed, 'work')}.`;
      error = null;
      editing = null;
      await load();
    } catch (e) {
      error = (e as Error).message;
    } finally {
      busy = false;
    }
  }

  const libraryLink = (path: string) => `/library?q=${encodeURIComponent(`tag:${path}`)}`;
</script>

<svelte:window onkeydown={(e) => e.key === 'Escape' && (editing = null)} />

{#snippet node(item: TagNode)}
  <li>
    <div class="row">
      <a href={libraryLink(item.path)}>{item.name}</a>
      <span class="muted count">{plural(item.works, 'work')}</span>
      <span class="actions">
        <button type="button" onclick={() => start(item.path, 'rename')} aria-label={`Rename ${item.path}`}>Rename</button>
        <button type="button" onclick={() => start(item.path, 'merge')} aria-label={`Merge ${item.path}`}>Merge</button>
      </span>
    </div>
    {#if editing?.path === item.path}
      <form class="edit" onsubmit={submit}>
        <label>
          {editing.action === 'rename' ? 'New name' : 'Merge into'}
          <input type="text" bind:value={editing.text} required autocomplete="off" />
        </label>
        <button type="submit" class="primary" disabled={busy}>{editing.action === 'rename' ? 'Rename tag' : 'Merge tag'}</button>
        <button type="button" onclick={() => (editing = null)}>Cancel</button>
      </form>
    {/if}
    {#if item.children.length}
      <ul>
        {#each item.children as child (child.path)}{@render node(child)}{/each}
      </ul>
    {/if}
  </li>
{/snippet}

<div class="page">
  <h1>Tags</h1>

  {#if message}
    <p class="done" role="status">{message} Undo with <code>shelf undo</code>.</p>
  {/if}
  {#if error}<p role="alert">{error}</p>{/if}

  {#if tree}
    {#if tree.tags === 0}
      <section class="empty">
        <h2>No tags yet</h2>
        <p class="muted">Tag a book on its page, or with <code>shelf tag add</code>.</p>
      </section>
    {:else}
      <p class="muted summary">{plural(tree.tags, 'tag')}. A tag with a slash is below the tag before it, for example <code>cloud/azure</code>.</p>
      <ul class="tree" aria-label="Tags">
        {#each tree.roots as root (root.path)}{@render node(root)}{/each}
      </ul>
    {/if}
  {:else if !error}
    <p class="muted">Loading…</p>
  {/if}
</div>

<style>
  .page {
    max-width: 52rem;
    margin: 0 auto;
    padding: 1.25rem var(--gutter) 4rem;
  }

  .summary {
    margin: 0.5rem 0 1.25rem;
  }

  .done {
    margin: 1rem 0;
    border-left: 3px solid var(--accent);
    background: var(--surface);
    padding: 0.6rem 0.85rem;
  }

  ul {
    list-style: none;
    margin: 0;
    padding-left: 1.25rem;
  }

  .tree {
    padding-left: 0;
  }

  .row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.25rem 0.75rem;
    padding: 0.35rem 0;
    border-top: 1px solid var(--line);
  }

  .count {
    font-size: 0.9rem;
  }

  .actions {
    display: flex;
    gap: 0.5rem;
    margin-left: auto;
  }

  .edit {
    display: flex;
    flex-wrap: wrap;
    align-items: end;
    gap: 0.5rem 0.75rem;
    padding: 0.5rem 0 0.75rem;
  }

  .edit label {
    display: grid;
    gap: 0.25rem;
    flex: 1 1 14rem;
  }
</style>
