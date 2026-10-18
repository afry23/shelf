<script lang="ts">
  import { coverUrl } from '../api';
  import { authorLine, clothOf } from '../format';

  // The cover of a work, or a spine when there is none: the title set in the serif on a cloth color that belongs to the work.
  let { id, title, authors, hasCover }: { id: string; title: string; authors: string[]; hasCover: boolean } = $props();
</script>

{#if hasCover}
  <img class="cover" src={coverUrl(id)} alt="" loading="lazy" decoding="async" />
{:else}
  <div class="cover spine" data-spine style:--cloth="var(--cloth-{clothOf(id)})" aria-hidden="true">
    <span class="title">{title}</span>
    {#if authors.length}<span class="author">{authorLine(authors)}</span>{/if}
  </div>
{/if}

<style>
  .cover {
    display: block;
    width: 100%;
    aspect-ratio: 2 / 3;
    object-fit: cover;
    background: var(--line);
    border-radius: 0.2rem 0.35rem 0.35rem 0.2rem;
    box-shadow:
      0 1px 2px rgb(0 0 0 / 0.25),
      inset 3px 0 0 rgb(255 255 255 / 0.08);
  }

  .spine {
    display: flex;
    flex-direction: column;
    justify-content: space-between;
    gap: 0.5rem;
    padding: 0.75rem 0.7rem 0.7rem 0.95rem;
    background: var(--cloth);
    color: var(--cloth-ink);
    overflow: hidden;
  }

  .title {
    font-family: var(--serif);
    font-weight: 600;
    font-size: 1rem;
    line-height: 1.2;
    overflow-wrap: anywhere;
    display: -webkit-box;
    -webkit-line-clamp: 5;
    line-clamp: 5;
    -webkit-box-orient: vertical;
    overflow: hidden;
  }

  .author {
    font-size: 0.75rem;
    opacity: 0.85;
    overflow-wrap: anywhere;
  }
</style>
