<script lang="ts">
  import { editWork, readingList as loadReadingList, removeFromReadingList, type ReadingList } from '../api';
  import Cover from '../components/Cover.svelte';
  import { authorLine, plural } from '../format';

  let list = $state<ReadingList | null>(null);
  let error = $state<string | null>(null);

  async function load(): Promise<void> {
    try {
      list = await loadReadingList();
      error = null;
    } catch (e) {
      error = (e as Error).message;
    }
  }

  $effect(() => {
    void load();
  });

  // Marking a work as read takes it off the list on the server; the list is read again to show what is left.
  async function markRead(id: string): Promise<void> {
    try {
      await editWork(id, { readStatus: 'read' });
      await load();
    } catch (e) {
      error = (e as Error).message;
    }
  }

  async function remove(id: string): Promise<void> {
    try {
      await removeFromReadingList(id);
      await load();
    } catch (e) {
      error = (e as Error).message;
    }
  }
</script>

<div class="page">
  <h1>Reading list</h1>

  {#if error}<p role="alert">{error}</p>{/if}

  {#if list}
    {#if list.total === 0}
      <section class="empty">
        <h2>Your reading list is empty</h2>
        <p class="muted">Put a book, a paper or an article on it from its page. A work leaves the list when you mark it as read.</p>
      </section>
    {:else}
      <p class="muted count">{plural(list.total, 'work')} to read, the oldest first.</p>
      <ul class="entries" aria-label="Reading list">
        {#each list.works as work (work.id)}
          <li>
            <article data-nav>
              <div class="cover-box"><Cover id={work.id} title={work.title} authors={work.authors} hasCover={work.hasCover} /></div>
              <div class="text">
                <h2><a href={`/works/${work.id}`}>{work.title}</a></h2>
                {#if work.authors.length}<p class="muted">{authorLine(work.authors)}</p>{/if}
                {#if work.readStatus === 'reading'}<p class="muted">Being read</p>{/if}
                <div class="actions">
                  <button type="button" onclick={() => markRead(work.id)} aria-label={`Mark as read: ${work.title}`}>Mark as read</button>
                  <button type="button" onclick={() => remove(work.id)} aria-label={`Remove from the reading list: ${work.title}`}>Remove</button>
                </div>
              </div>
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
    display: grid;
    grid-template-columns: 5rem 1fr;
    gap: 1rem;
    background: var(--surface);
    border: 1px solid var(--line);
    border-radius: var(--radius);
    padding: 1rem;
  }

  .text {
    display: grid;
    align-content: start;
    gap: 0.4rem;
  }

  h2 {
    font-size: 1.1rem;
  }

  .actions {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
    margin-top: 0.25rem;
  }
</style>
