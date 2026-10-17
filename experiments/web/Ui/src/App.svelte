<script lang="ts">
  interface Book {
    id: string;
    title: string;
    authors: string[];
  }

  let book = $state<Book | null>(null);
  let error = $state<string | null>(null);

  $effect(() => {
    fetch('/api/v1/books/B1')
      .then((r) => (r.ok ? r.json() : Promise.reject(new Error(`HTTP ${r.status}`))))
      .then((b: Book) => (book = b))
      .catch((e: Error) => (error = e.message));
  });
</script>

<h1>shelf</h1>
{#if book}
  <p>{book.title} by {book.authors.join(', ')}</p>
{:else if error}
  <p>Error: {error}</p>
{:else}
  <p>Loading…</p>
{/if}
