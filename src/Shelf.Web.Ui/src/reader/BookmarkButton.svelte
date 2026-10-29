<script lang="ts">
  import type { Annotations } from './annotations.svelte';

  // Sets a bookmark at the place the reader is at, or removes the one that is there (F-RDR-6). `locator` is null while the place is not known.
  let { store, fileId, locator }: { store: Annotations; fileId: string; locator: string | null } = $props();

  const bookmark = $derived(store.items.find((a) => a.kind === 'bookmark' && a.file === fileId && a.locator === locator));

  async function toggle(): Promise<void> {
    if (bookmark) {
      await store.remove(bookmark);
    } else if (locator) {
      await store.add(fileId, { kind: 'bookmark', locator });
    }
  }
</script>

<button type="button" class="button" aria-pressed={bookmark !== undefined} disabled={!locator} onclick={toggle}>
  {bookmark ? 'Remove bookmark' : 'Add bookmark'}
</button>
