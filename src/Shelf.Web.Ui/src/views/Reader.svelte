<script lang="ts">
  import { fileUrl } from '../api';
  import { FileIds } from '../fileids';

  let { fileId }: { fileId: string } = $props();
  const parts = $derived(FileIds.split(fileId));
</script>

<div class="page">
  <section class="notice">
    <h1>The reader is not ready yet</h1>
    <p>This book will open here, at the place you came from. Until then you can download the file.</p>
    <p class="actions">
      <a class="button primary" href={fileUrl(fileId, true)} download>Download {parts?.name ?? 'the file'}</a>
      {#if parts}<a class="button" href={`/works/${parts.work}`}>Back to the book</a>{/if}
    </p>
  </section>
</div>

<style>
  .page {
    max-width: 40rem;
    margin: 0 auto;
    padding: 3rem var(--gutter);
  }

  h1 {
    margin-bottom: 0.75rem;
  }

  .actions {
    display: flex;
    gap: 0.75rem;
    margin-top: 1.25rem;
  }
</style>
