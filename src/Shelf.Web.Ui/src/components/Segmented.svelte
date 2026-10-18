<script lang="ts" generics="T extends string">
  // A row of buttons of which one is pressed: "radio buttons" that are buttons, so that each has a name and aria-pressed.
  let {
    label,
    options,
    value,
    onchange,
  }: { label: string; options: { value: T; label: string }[]; value: T; onchange: (value: T) => void } = $props();
</script>

<div class="segmented" role="group" aria-label={label}>
  {#each options as option (option.value)}
    <button type="button" aria-pressed={value === option.value} onclick={() => onchange(option.value)}>{option.label}</button>
  {/each}
</div>

<style>
  .segmented {
    display: inline-flex;
    border: 1px solid var(--line);
    border-radius: var(--radius);
    overflow: hidden;
    background: var(--surface);
  }

  button {
    border: 0;
    background: transparent;
    padding: 0.35rem 0.8rem;
    border-right: 1px solid var(--line);
  }

  button:last-child {
    border-right: 0;
  }

  button[aria-pressed='true'] {
    background: var(--accent);
    color: var(--accent-ink);
  }
</style>
