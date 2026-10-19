<script lang="ts">
  import { handleLinkClick, navigate, param, router } from './router.svelte';
  import { duplicateCount, refreshDuplicateCount } from './stores.svelte';
  import Duplicates from './views/Duplicates.svelte';
  import Library from './views/Library.svelte';
  import Reader from './views/Reader.svelte';
  import Review from './views/Review.svelte';
  import Search from './views/Search.svelte';
  import Tags from './views/Tags.svelte';
  import Work from './views/Work.svelte';

  // The route is the path: /library, /search, /works/{id}, /tags, /review, /duplicates, /read/{fileId}. The root leads to the library.
  const route = $derived.by(() => {
    const path = router.path.replace(/\/+$/, '') || '/';
    const segments = path.split('/').filter(Boolean).map(decodeURIComponent);
    switch (segments[0]) {
      case undefined:
      case 'library':
        return { name: 'library' } as const;
      case 'search':
        return { name: 'search' } as const;
      case 'review':
        return { name: 'review' } as const;
      case 'tags':
        return { name: 'tags' } as const;
      case 'duplicates':
        return { name: 'duplicates' } as const;
      case 'works':
        return segments[1] ? ({ name: 'work', id: segments[1] } as const) : ({ name: 'missing' } as const);
      case 'read':
        return segments[1] ? ({ name: 'read', id: segments[1] } as const) : ({ name: 'missing' } as const);
      default:
        return { name: 'missing' } as const;
    }
  });

  $effect(() => {
    if (router.path === '/' || router.path === '') {
      navigate('/library' + router.search, { replace: true });
    }
  });

  $effect(() => {
    void refreshDuplicateCount();
  });

  // The field of the header shows the query while a search page is open.
  let text = $state('');
  $effect(() => {
    text = route.name === 'search' ? param('q') : '';
  });

  function submit(event: SubmitEvent): void {
    event.preventDefault();
    const q = text.trim();
    if (!q) {
      return;
    }
    const mode = route.name === 'search' ? param('mode') : '';
    const params = new URLSearchParams({ q });
    if (mode) {
      params.set('mode', mode);
    }
    navigate(`/search?${params.toString()}`);
  }

  const here = (name: string) => (route.name === name ? 'page' : undefined);
</script>

<svelte:document onclick={handleLinkClick} />

<div class="app" class:fill={route.name === 'read'}>
<header class="top">
  <a class="brand" href="/library">shelf</a>
  <form class="search" role="search" onsubmit={submit}>
    <input type="search" aria-label="Search your library" placeholder="Search your library" bind:value={text} />
  </form>
  <nav aria-label="Main">
    <a href="/library" aria-current={here('library')}>Library</a>
    <a href="/tags" aria-current={here('tags')}>Tags</a>
    <a href="/review" aria-current={here('review')}>Review</a>
    <a href="/duplicates" aria-current={here('duplicates')}>
      Duplicates{#if duplicateCount.value}<span class="badge">{duplicateCount.value}</span>{/if}
    </a>
  </nav>
</header>

<main>
  {#if route.name === 'library'}
    <Library />
  {:else if route.name === 'search'}
    <Search />
  {:else if route.name === 'review'}
    <Review />
  {:else if route.name === 'tags'}
    <Tags />
  {:else if route.name === 'duplicates'}
    <Duplicates />
  {:else if route.name === 'work'}
    <Work id={route.id} />
  {:else if route.name === 'read'}
    <Reader fileId={route.id} />
  {:else}
    <div class="missing">
      <h1>There is nothing at this address</h1>
      <p><a href="/library">Back to the library</a></p>
    </div>
  {/if}
</main>
</div>

<style>
  .app {
    display: flex;
    flex-direction: column;
    min-height: 100dvh;
  }

  /* The reader takes the window below the header; the page itself does not scroll, the book does. */
  .app.fill {
    height: 100dvh;
  }

  main {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
  }

  main > :global(.page) {
    width: 100%;
  }

  .top {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 0.75rem 1.5rem;
    padding: 0.7rem var(--gutter);
    border-bottom: 1px solid var(--line);
    background: var(--surface);
  }

  .brand {
    font-family: var(--serif);
    font-size: 1.5rem;
    font-weight: 700;
    letter-spacing: -0.01em;
    color: var(--accent);
    text-decoration: none;
  }

  .search {
    flex: 1 1 14rem;
    max-width: 30rem;
  }

  .search input {
    width: 100%;
  }

  nav {
    display: flex;
    gap: 1.25rem;
    margin-left: auto;
  }

  nav a {
    color: var(--ink);
    text-decoration: none;
    padding: 0.2rem 0;
    border-bottom: 2px solid transparent;
  }

  nav a[aria-current='page'] {
    border-bottom-color: var(--accent);
  }

  nav a:hover {
    border-bottom-color: var(--line);
  }

  .badge {
    display: inline-block;
    min-width: 1.4em;
    margin-left: 0.4em;
    padding: 0 0.4em;
    border-radius: 999px;
    background: var(--accent);
    color: var(--accent-ink);
    font-size: 0.8rem;
    text-align: center;
  }

  .missing {
    max-width: 40rem;
    margin: 0 auto;
    padding: 3rem var(--gutter);
  }

  .missing h1 {
    margin-bottom: 0.75rem;
  }
</style>
