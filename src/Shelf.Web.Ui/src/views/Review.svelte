<script lang="ts">
  import { acceptReview, dismissReview, review as loadReview, type ReviewCandidate, type ReviewEntry, type ReviewList } from '../api';
  import { plural } from '../format';

  let list = $state<ReviewList | null>(null);
  let error = $state<string | null>(null);
  let message = $state<string | null>(null);
  // The merge that waits for a second click: a merge cannot be undone with shelf undo, so nothing moves before it.
  let pending = $state<{ entry: string; work: string } | null>(null);

  async function load(): Promise<void> {
    try {
      list = await loadReview();
      error = null;
    } catch (e) {
      error = (e as Error).message;
    }
  }

  $effect(() => {
    void load();
  });

  async function accept(entry: ReviewEntry, candidate: ReviewCandidate): Promise<void> {
    pending = null;
    try {
      const done = await acceptReview(entry.id, candidate.work);
      message = `Merged into ${candidate.title ?? candidate.work}: ${plural(done.movedFiles, 'file')} moved. A merge cannot be undone with shelf undo.`;
      error = null;
      await load();
    } catch (e) {
      error = (e as Error).message;
    }
  }

  async function dismiss(entry: ReviewEntry): Promise<void> {
    pending = null;
    try {
      await dismissReview(entry.id);
      message = entry.kind === 'grouping' ? 'Kept apart. They are not suggested again.' : 'Dismissed.';
      error = null;
      await load();
    } catch (e) {
      error = (e as Error).message;
    }
  }

  const kindLabel = (kind: ReviewEntry['kind']) =>
    kind === 'grouping' ? 'May belong to another book' : kind === 'extraction-error' ? 'Could not be read' : 'Weak metadata';
  const best = (entry: ReviewEntry) => entry.candidates.reduce((a, c) => (c.confidence > a ? c.confidence : a), 0);
</script>

<svelte:window onkeydown={(e) => e.key === 'Escape' && (pending = null)} />

<div class="page">
  <h1>Review</h1>

  {#if message}<p class="done" role="status">{message}</p>{/if}
  {#if error}<p role="alert">{error}</p>{/if}

  {#if list}
    {#if list.total === 0}
      <section class="empty">
        <h2>Nothing to review</h2>
        <p class="muted">The import was sure about every file.</p>
      </section>
    {:else}
      <p class="muted count">{plural(list.total, 'entry', 'entries')} to decide.</p>
      <ul class="entries" aria-label="Review">
        {#each list.entries as entry (entry.id)}
          <li>
            <article>
              <h2>{kindLabel(entry.kind)}</h2>
              {#if entry.file}<p class="path">{entry.file}</p>{/if}
              {#if entry.kind === 'grouping' && entry.work}
                <p>
                  The file belongs to the book
                  <a href={`/works/${entry.work}`}>{entry.workTitle ?? entry.work}</a>. It may be another edition of one of these:
                </p>
                <ul class="candidates">
                  {#each entry.candidates as candidate (candidate.work)}
                    <li>
                      <a href={`/works/${candidate.work}`}>{candidate.title ?? candidate.work}</a>
                      <span class="muted">{Math.round(candidate.confidence * 100)} %{candidate.signals.length ? `, ${candidate.signals.join(', ')}` : ''}</span>
                      {#if pending?.entry === entry.id && pending.work === candidate.work}
                        <button type="button" class="button danger" onclick={() => accept(entry, candidate)}>Confirm: merge into this book</button>
                        <button type="button" onclick={() => (pending = null)}>Cancel</button>
                      {:else}
                        <button type="button" onclick={() => (pending = { entry: entry.id, work: candidate.work })} aria-label={`Merge into ${candidate.title ?? candidate.work}`}>Merge into this book</button>
                      {/if}
                    </li>
                  {/each}
                </ul>
                <div class="actions">
                  <button type="button" onclick={() => dismiss(entry)}>Keep apart</button>
                </div>
              {:else}
                <p>{entry.message}</p>
                <div class="actions">
                  <button type="button" onclick={() => dismiss(entry)}>Dismiss</button>
                </div>
              {/if}
            </article>
          </li>
        {/each}
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

  .count {
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
    padding: 0;
  }

  .entries {
    display: grid;
    gap: 1rem;
  }

  article {
    background: var(--surface);
    border: 1px solid var(--line);
    border-radius: var(--radius);
    padding: 1rem;
    display: grid;
    gap: 0.5rem;
  }

  h2 {
    font-size: 1.1rem;
  }

  .path {
    color: var(--muted);
    font-size: 0.85rem;
    overflow-wrap: anywhere;
  }

  .candidates li {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.25rem 0.75rem;
    padding: 0.4rem 0;
    border-top: 1px solid var(--line);
  }

  .actions {
    display: flex;
    gap: 0.5rem;
  }
</style>
