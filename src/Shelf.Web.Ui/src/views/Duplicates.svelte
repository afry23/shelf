<script lang="ts">
  import { duplicateAction, duplicates, ignoreProbable, probableDuplicates, type DuplicateFile, type DuplicateList, type ProbableList, type ProbablePair } from '../api';
  import { authorLine, bytes, plural } from '../format';
  import { refreshDuplicateCount } from '../stores.svelte';

  let list = $state<DuplicateList | null>(null);
  // Works that probably are one book: for a look, never for the trash. Without any, the section stays away.
  let probable = $state<ProbableList | null>(null);
  let error = $state<string | null>(null);
  let message = $state<string | null>(null);
  // The action that waits for a second click: the files of this run are not touched before.
  let pending = $state<{ fileId: string; action: 'keep' | 'trash' } | null>(null);

  async function load(): Promise<void> {
    try {
      list = await duplicates();
      probable = await probableDuplicates().catch(() => null);
      error = null;
      void refreshDuplicateCount();
    } catch (e) {
      error = (e as Error).message;
    }
  }

  $effect(() => {
    void load();
  });

  async function run(action: 'keep' | 'trash' | 'ignore', fileId: string): Promise<void> {
    pending = null;
    try {
      const outcome = await duplicateAction(action, fileId);
      message =
        action === 'ignore'
          ? 'Marked as not duplicates.'
          : `Moved ${plural(outcome.trashed, 'file')} to the trash.` +
            (outcome.worksRemoved.length ? ` ${plural(outcome.worksRemoved.length, 'work')} ${outcome.worksRemoved.length === 1 ? 'was' : 'were'} removed.` : '');
      error = null;
      await load();
    } catch (e) {
      error = (e as Error).message;
    }
  }

  async function notDuplicates(pair: ProbablePair): Promise<void> {
    try {
      await ignoreProbable(pair.a.id, pair.b.id);
      message = 'Marked as not duplicates.';
      error = null;
      await load();
    } catch (e) {
      error = (e as Error).message;
    }
  }

  const reason = (pair: ProbablePair) =>
    [pair.hamming !== null && `Same text (distance ${pair.hamming})`, pair.cosine !== null && `Similar meaning (${pair.cosine.toFixed(2)})`]
      .filter(Boolean)
      .join(', ');

  const ask = (fileId: string, action: 'keep' | 'trash') => (pending = { fileId, action });
  const asks = (file: DuplicateFile, action: 'keep' | 'trash') => pending?.fileId === file.id && pending.action === action;
</script>

<svelte:window onkeydown={(e) => e.key === 'Escape' && (pending = null)} />

<div class="page">
  <h1>Duplicates</h1>

  {#if message}
    <p class="done" role="status">{message}{#if !message.startsWith('Marked')} Undo with <code>shelf undo</code>.{/if}</p>
  {/if}
  {#if error}<p role="alert">{error}</p>{/if}

  {#if list}
    {#if list.total === 0 && !probable?.total}
      <section class="empty">
        <h2>No duplicates</h2>
        <p class="muted">Every file in the library is a book of its own.</p>
      </section>
    {:else if list.total > 0}
      <p class="muted count" role="status">{plural(list.total, 'group')} of duplicates</p>
      {#each list.groups as group, index (group.files[0].id)}
        <section class="group" aria-labelledby={`group-${index}`}>
          <h2 id={`group-${index}`}>{group.kind === 'identical' ? 'Identical files' : 'Different files of one book'}</h2>
          <p class="muted">
            {group.kind === 'identical' ? 'These files have the same content.' : `These are different ${group.format} files of one book.`}
          </p>
          <div class="cards">
            {#each group.files as file, position (file.id)}
              <article aria-label={file.name} data-suggested={position === group.keep} data-nav>
                <h3><a href={`/works/${file.workId}`}>{file.title}</a></h3>
                <p class="path">{file.path}</p>
                <dl>
                  <dt>Size</dt><dd>{bytes(file.sizeBytes)}</dd>
                  {#if file.pages !== null}<dt>Pages</dt><dd>{file.pages}</dd>{/if}
                  {#if file.completeness !== null}<dt>Details</dt><dd>{file.completeness} of 8</dd>{/if}
                </dl>
                {#if position === group.keep}<p class="suggested">Suggested to keep</p>{/if}
                <div class="actions">
                  {#if asks(file, 'keep')}
                    <button type="button" class="button danger" onclick={() => run('keep', file.id)}>Confirm</button>
                    <button type="button" class="button" onclick={() => (pending = null)}>Cancel</button>
                  {:else if asks(file, 'trash')}
                    <button type="button" class="button danger" onclick={() => run('trash', file.id)}>Confirm</button>
                    <button type="button" class="button" onclick={() => (pending = null)}>Cancel</button>
                  {:else}
                    <button type="button" class={['button', position === group.keep && 'primary']} onclick={() => ask(file.id, 'keep')}>Keep this file</button>
                    <button type="button" class="button" onclick={() => ask(file.id, 'trash')}>Move to trash</button>
                  {/if}
                </div>
              </article>
            {/each}
          </div>
          <p><button type="button" class="button" onclick={() => run('ignore', group.files[0].id)}>These are not duplicates</button></p>
        </section>
      {/each}
    {/if}
    {#if probable?.total}
      <section class="group" aria-labelledby="probable-heading">
        <h2 id="probable-heading">Probable duplicates</h2>
        <p class="muted" role="status">{plural(probable.total, 'pair')} of works that probably are one book. Nothing here is removed: have a look and say when they are not.</p>
        {#each probable.pairs as pair (pair.a.id + pair.b.id)}
          <div class="pair">
            <p class="reason">{reason(pair)}</p>
            <div class="cards">
              {#each [pair.a, pair.b] as work (work.id)}
                <article aria-label={work.title} data-nav>
                  <h3><a href={`/works/${work.id}`}>{work.title}</a></h3>
                  {#if work.authors.length}<p class="muted">{authorLine(work.authors)}</p>{/if}
                  <p class="path">{work.folder}</p>
                </article>
              {/each}
            </div>
            <p><button type="button" class="button" onclick={() => notDuplicates(pair)}>Not a duplicate</button></p>
          </div>
        {/each}
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

  .count {
    margin: 0.5rem 0 1.5rem;
  }

  .done {
    margin: 1rem 0;
    border-left: 3px solid var(--accent);
    background: var(--surface);
    padding: 0.6rem 0.85rem;
  }

  .empty {
    padding: 2rem 0;
  }

  .group {
    padding: 1.25rem 0 1.5rem;
    border-top: 1px solid var(--line);
  }

  .group > p {
    margin: 0.25rem 0 1rem;
  }

  .cards {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(15rem, 1fr));
    gap: 1rem;
    margin-bottom: 1rem;
  }

  article {
    background: var(--surface);
    border: 1px solid var(--line);
    border-radius: var(--radius);
    padding: 1rem;
    display: grid;
    gap: 0.5rem;
    align-content: start;
    min-width: 0;
  }

  article[data-suggested='true'] {
    border-color: var(--accent);
    box-shadow: inset 3px 0 0 var(--accent);
  }

  .path {
    color: var(--muted);
    font-size: 0.85rem;
    overflow-wrap: anywhere;
  }

  dl {
    display: grid;
    grid-template-columns: max-content 1fr;
    gap: 0.15rem 1rem;
    margin: 0;
    font-size: 0.9rem;
  }

  dt {
    color: var(--muted);
  }

  dd {
    margin: 0;
  }

  .suggested {
    color: var(--accent);
    font-size: 0.875rem;
    font-weight: 600;
  }

  .pair {
    padding: 0.5rem 0 0.75rem;
  }

  .reason {
    color: var(--accent);
    font-size: 0.875rem;
    font-weight: 600;
    margin: 0 0 0.5rem;
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
    margin-top: 0.25rem;
  }
</style>
