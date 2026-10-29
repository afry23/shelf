<script lang="ts">
  import { libraryStats, type LibraryStats, type ReadingItem } from '../api';
  import { plural } from '../format';

  // What was read and when (F-STA-3): the books set to read by month, the books being read and the ones a reader moved in last. The
  // numbers are counted by the server; this view only lays them out.
  let stats = $state<LibraryStats | null>(null);
  let error = $state<string | null>(null);

  $effect(() => {
    libraryStats()
      .then((result) => (stats = result))
      .catch((e: Error) => (error = e.message));
  });

  /** The months from the first to the last with a book, a month without one as 0, and at most the latest 24. */
  const months = $derived.by(() => {
    const counted = new Map((stats?.finishedByMonth ?? []).map((m) => [m.month, m.works]));
    const keys = [...counted.keys()].sort();
    if (keys.length === 0) {
      return [];
    }
    const [firstYear, firstMonth] = keys[0].split('-').map(Number);
    const [lastYear, lastMonth] = keys[keys.length - 1].split('-').map(Number);
    const all: { month: string; works: number }[] = [];
    for (let y = firstYear, m = firstMonth; y < lastYear || (y === lastYear && m <= lastMonth); m === 12 ? ((y += 1), (m = 1)) : (m += 1)) {
      const key = `${y}-${String(m).padStart(2, '0')}`;
      all.push({ month: key, works: counted.get(key) ?? 0 });
    }
    return all.slice(-24);
  });

  const most = $derived(Math.max(1, ...months.map((m) => m.works)));
  const empty = $derived(
    stats !== null && stats.finishedByMonth.length === 0 && stats.readWithoutDate === 0 && stats.reading.length === 0 && stats.recent.length === 0,
  );

  const day = (time: string | null) => (time ? time.slice(0, 10) : '');
</script>

{#snippet works(items: ReadingItem[], label: string)}
  <ul class="works" aria-label={label}>
    {#each items as item (item.id)}
      <li>
        <a href={`/works/${item.id}`}>{item.title}</a>
        <span class="muted">{day(item.lastReadAt)}</span>
      </li>
    {/each}
  </ul>
{/snippet}

<div class="page">
  <h1>Statistics</h1>

  {#if error}
    <p role="alert">{error}</p>
  {:else if !stats}
    <p class="muted">Loading…</p>
  {:else if empty}
    <p class="muted empty">Nothing read yet. Set a book to Read on its page and it shows up here.</p>
  {:else}
    <p class="muted summary">
      {stats.readStatus.read} read, {stats.readStatus.reading} reading, {stats.readStatus.unread} unread
    </p>

    {#if months.length}
      <section class="group">
        <h2>Finished by month</h2>
        <ul class="months" aria-label="Finished by month">
          {#each months as month (month.month)}
            <li>
              <span class="month">{month.month}</span>
              <span class="bar" aria-hidden="true"><span style:width="{(month.works / most) * 100}%"></span></span>
              <span class="value">{month.works}</span>
            </li>
          {/each}
        </ul>
      </section>
    {/if}

    {#if stats.readWithoutDate > 0}
      <p class="muted">
        {plural(stats.readWithoutDate, 'book')} {stats.readWithoutDate === 1 ? 'is' : 'are'} marked as read without a date. Set
        {stats.readWithoutDate === 1 ? 'it' : 'them'} to Read again to date {stats.readWithoutDate === 1 ? 'it' : 'them'}.
      </p>
    {/if}

    {#if stats.reading.length}
      <section class="group">
        <h2>Reading</h2>
        {@render works(stats.reading, 'Reading')}
      </section>
    {/if}

    {#if stats.recent.length}
      <section class="group">
        <h2>Recently read</h2>
        {@render works(stats.recent, 'Recently read')}
      </section>
    {/if}
  {/if}
</div>

<style>
  .page {
    max-width: 48rem;
    margin: 0 auto;
    padding: 1.25rem var(--gutter) 4rem;
  }

  .summary,
  .empty {
    margin: 0.5rem 0 1.5rem;
  }

  .group {
    border-top: 1px solid var(--line);
    padding: 1rem 0 1.25rem;
  }

  /* One row per month: the name, a bar as long as the count compared with the best month, and the count. */
  .months,
  .works {
    list-style: none;
    margin: 0;
    padding: 0;
    display: grid;
    gap: 0.35rem;
  }

  .months li {
    display: grid;
    grid-template-columns: 5rem 1fr 2rem;
    align-items: center;
    gap: 0.75rem;
  }

  .month,
  .value {
    font-variant-numeric: tabular-nums;
  }

  .value {
    text-align: right;
  }

  .bar {
    display: block;
    height: 0.9rem;
    background: var(--surface);
    border-radius: 2px;
  }

  .bar span {
    display: block;
    height: 100%;
    background: var(--accent);
    border-radius: 2px;
  }

  .works li {
    display: flex;
    justify-content: space-between;
    gap: 1rem;
  }
</style>
