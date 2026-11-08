<script lang="ts">
  // A modal dialog on the native <dialog>: Escape, focus trap and the role come from the browser.
  import type { Snippet } from 'svelte';

  let { title, onclose, busy = false, children }: { title: string; onclose: () => void; busy?: boolean; children: Snippet } = $props();

  let element = $state<HTMLDialogElement | null>(null);
  const titleId = `dialog-${Math.random().toString(36).slice(2, 8)}`;

  $effect(() => {
    const dialog = element;
    if (dialog && !dialog.open) {
      dialog.showModal();
    }
    return () => dialog?.close();
  });

  // What is being sent must not be walked away from.
  function cancel(event: Event): void {
    event.preventDefault();
    if (!busy) {
      onclose();
    }
  }

  function backdrop(event: MouseEvent): void {
    if (event.target === element && !busy) {
      onclose();
    }
  }
</script>

<!-- svelte-ignore a11y_click_events_have_key_events, a11y_no_noninteractive_element_interactions -->
<dialog bind:this={element} aria-labelledby={titleId} oncancel={cancel} onclick={backdrop}>
  <div class="frame">
    <h2 id={titleId}>{title}</h2>
    {@render children()}
  </div>
</dialog>

<style>
  dialog {
    width: min(32rem, calc(100vw - 2rem));
    max-height: calc(100vh - 2rem);
    padding: 0;
    border: 1px solid var(--line);
    border-radius: calc(var(--radius) * 2);
    background: var(--surface);
    color: var(--ink);
    box-shadow: 0 1.5rem 3rem rgb(0 0 0 / 0.25);
  }

  dialog::backdrop {
    background: rgb(20 28 25 / 0.45);
  }

  .frame {
    padding: 1.25rem 1.5rem 1.5rem;
  }

  h2 {
    margin: 0 0 1rem;
    font-family: var(--serif);
    font-size: 1.35rem;
  }
</style>
