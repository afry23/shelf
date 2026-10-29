<script lang="ts">
  import type { Snippet } from 'svelte';

  // Tabs after the WAI-ARIA pattern: one tab is in the tab order, the arrow keys move between the tabs and open them. The parent
  // keeps which tab is open and renders only that panel.
  let {
    label,
    tabs,
    active,
    onselect,
    children,
  }: {
    label: string;
    tabs: { id: string; label: string; count?: number }[];
    active: string;
    onselect: (id: string) => void;
    children: Snippet;
  } = $props();

  function move(event: KeyboardEvent, index: number): void {
    const target =
      event.key === 'ArrowRight' ? (index + 1) % tabs.length
      : event.key === 'ArrowLeft' ? (index - 1 + tabs.length) % tabs.length
      : event.key === 'Home' ? 0
      : event.key === 'End' ? tabs.length - 1
      : -1;
    if (target < 0) {
      return;
    }
    event.preventDefault();
    onselect(tabs[target].id);
    document.getElementById(`tab-${tabs[target].id}`)?.focus();
  }
</script>

<div class="tabs">
  <div class="list" role="tablist" aria-label={label}>
    {#each tabs as tab, index (tab.id)}
      <button
        type="button"
        role="tab"
        id={`tab-${tab.id}`}
        aria-selected={tab.id === active}
        aria-controls={`panel-${tab.id}`}
        tabindex={tab.id === active ? 0 : -1}
        onclick={() => onselect(tab.id)}
        onkeydown={(e) => move(e, index)}
      >
        <span>{tab.label}</span>{#if tab.count !== undefined}<span class="count">{tab.count}</span>{/if}
      </button>
    {/each}
  </div>
  <div class="panel" role="tabpanel" id={`panel-${active}`} aria-labelledby={`tab-${active}`} tabindex="0">
    {@render children()}
  </div>
</div>

<style>
  /* One rule under the tabs; the open tab sits on it with a heavier line in the accent, like a bookmark in the book. */
  .list {
    display: flex;
    gap: 0.25rem;
    border-bottom: 1px solid var(--line);
    overflow-x: auto;
    scrollbar-width: thin;
  }

  button {
    flex: none;
    display: inline-flex;
    align-items: baseline;
    gap: 0.4rem;
    white-space: nowrap;
    background: transparent;
    border: 0;
    border-bottom: 2px solid transparent;
    border-radius: 0;
    margin-bottom: -1px;
    padding: 0.55rem 0.9rem;
    color: var(--muted);
  }

  button:hover {
    color: var(--ink);
  }

  button[aria-selected='true'] {
    color: var(--ink);
    font-weight: 600;
    border-bottom-color: var(--accent);
  }

  button:focus-visible {
    outline: 2px solid var(--accent);
    outline-offset: -2px;
  }

  .count {
    font-size: 0.8125rem;
    font-weight: normal;
    font-variant-numeric: tabular-nums;
    color: var(--muted);
  }

  .panel {
    padding-top: 1.25rem;
    min-width: 0;
  }

  .panel:focus-visible {
    outline: 2px solid var(--accent);
    outline-offset: 4px;
  }
</style>
