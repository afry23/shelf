<script lang="ts">
  import { applyOrganize, previewOrganize, undoLast, type OrganizePreview } from '../api';
  import { plural } from '../format';

  let preview = $state<OrganizePreview | null>(null);
  let error = $state<string | null>(null);
  let message = $state<string | null>(null);
  let notes = $state<string[]>([]);
  // The pattern in the field; the preview shows the one that applies.
  let pattern = $state('');
  let confirming = $state(false);
  let busy = $state(false);

  async function load(text?: string): Promise<void> {
    try {
      preview = await previewOrganize(text);
      pattern = preview.pattern;
      error = null;
    } catch (e) {
      error = (e as Error).message;
    }
  }

  $effect(() => {
    void load();
  });

  async function tryPattern(event: SubmitEvent): Promise<void> {
    event.preventDefault();
    confirming = false;
    await load(pattern.trim() || undefined);
  }

  async function apply(): Promise<void> {
    if (!preview || busy) {
      return;
    }
    busy = true;
    confirming = false;
    try {
      const done = await applyOrganize(preview.pattern);
      message = `Moved ${plural(done.files, 'file')} of ${plural(done.works, 'work')}.`;
      notes = done.collisions.map((c) => `Left in place: ${c.from} (${c.reason})`);
      error = null;
      await load(preview.pattern);
    } catch (e) {
      error = (e as Error).message;
    } finally {
      busy = false;
    }
  }

  async function undo(): Promise<void> {
    if (busy) {
      return;
    }
    busy = true;
    try {
      const done = await undoLast();
      message = `Reverted the last ${done.label} run.`;
      notes = done.skipped.map((s) => `Kept ${s.path}: ${s.reason}`);
      error = null;
      await load(preview?.pattern);
    } catch (e) {
      error = (e as Error).message;
    } finally {
      busy = false;
    }
  }
</script>

<svelte:window onkeydown={(e) => e.key === 'Escape' && (confirming = false)} />

<div class="page">
  <h1>Organize</h1>

  {#if message}
    <div class="done" role="status">
      <p>{message}</p>
      {#each notes as note (note)}<p class="muted">{note}</p>{/each}
    </div>
  {/if}
  {#if error}<p role="alert">{error}</p>{/if}

  {#if preview}
    <form class="pattern" onsubmit={tryPattern}>
      <label>
        Pattern
        <input type="text" bind:value={pattern} autocomplete="off" spellcheck="false" />
      </label>
      <button type="submit">Preview</button>
    </form>

    {#if preview.files === 0 && preview.collisions.length === 0}
      <section class="empty">
        <h2>Everything is where the pattern puts it</h2>
        <p class="muted">{plural(preview.unchanged, 'file')} already in place.</p>
      </section>
    {:else}
      <p class="summary">
        {plural(preview.files, 'file')} of {plural(preview.works, 'work')} would move{preview.unchanged ? `; ${plural(preview.unchanged, 'file')} already in place` : ''}.
      </p>

      <div class="actions">
        {#if confirming}
          <button type="button" class="button danger" onclick={apply} disabled={busy}>Confirm: move {plural(preview.files, 'file')}</button>
          <button type="button" onclick={() => (confirming = false)}>Cancel</button>
        {:else}
          <button type="button" class="button primary" onclick={() => (confirming = true)} disabled={busy || preview.files === 0}>Move the files</button>
        {/if}
      </div>

      {#if preview.truncated}
        <p class="muted">The list shows the first {preview.moves.length} moves; all of them are made.</p>
      {/if}

      <ul class="moves" aria-label="Moves">
        {#each preview.moves as move (move.from)}
          <li>
            <span class="from">{move.from}</span>
            <span class="arrow" aria-hidden="true">→</span>
            <span class="to">{move.to}</span>
          </li>
        {/each}
      </ul>

      {#if preview.collisions.length}
        <h2>Left where they are</h2>
        <p class="muted">The target is taken. Nothing is overwritten.</p>
        <ul class="moves" aria-label="Collisions">
          {#each preview.collisions as collision (collision.from)}
            <li>
              <span class="from">{collision.from}</span>
              <span class="arrow" aria-hidden="true">→</span>
              <span class="to">{collision.to}</span>
              <span class="muted">{collision.reason}</span>
            </li>
          {/each}
        </ul>
      {/if}
    {/if}

    {#if preview.nextUndo}
      <section class="undo">
        <h2>Undo</h2>
        <p class="muted">The last change that can be taken back is a <strong>{preview.nextUndo}</strong> run.</p>
        <button type="button" onclick={undo} disabled={busy}>Undo {preview.nextUndo}</button>
      </section>
    {/if}
  {:else if !error}
    <p class="muted">Loading…</p>
  {/if}
</div>

<style>
  .page {
    max-width: 72rem;
    margin: 0 auto;
    padding: 1.25rem var(--gutter) 4rem;
  }

  .done {
    margin: 1rem 0;
    border-left: 3px solid var(--accent);
    background: var(--surface);
    padding: 0.6rem 0.85rem;
  }

  .pattern {
    display: flex;
    flex-wrap: wrap;
    align-items: end;
    gap: 0.5rem 0.75rem;
    margin: 1rem 0;
  }

  .pattern label {
    display: grid;
    gap: 0.25rem;
    flex: 1 1 20rem;
  }

  .pattern input {
    font-family: var(--mono, monospace);
  }

  .summary {
    margin: 1rem 0 0.75rem;
  }

  .actions {
    display: flex;
    gap: 0.5rem;
    margin-bottom: 1rem;
  }

  .moves {
    list-style: none;
    margin: 0 0 1.5rem;
    padding: 0;
  }

  .moves li {
    display: flex;
    flex-wrap: wrap;
    gap: 0.15rem 0.6rem;
    padding: 0.45rem 0;
    border-top: 1px solid var(--line);
    font-size: 0.9rem;
    overflow-wrap: anywhere;
  }

  .from {
    color: var(--muted);
  }

  .arrow {
    color: var(--accent);
  }

  .undo {
    margin-top: 2rem;
    padding-top: 1rem;
    border-top: 1px solid var(--line);
  }

  .empty {
    padding: 2rem 0;
  }
</style>
