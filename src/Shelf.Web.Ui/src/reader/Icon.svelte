<script lang="ts">
  // The small drawings of the reader's tools. They are decoration: the button that holds one carries the name (aria-label and title).
  export type IconName = 'prev' | 'next' | 'minus' | 'plus' | 'fit' | 'contents' | 'bookmark' | 'notes' | 'highlight' | 'quote' | 'up' | 'down' | 'display';
  let { name, filled = false }: { name: IconName; filled?: boolean } = $props();

  const paths: Record<IconName, string> = {
    prev: 'M14.5 5.5 8 12l6.5 6.5',
    next: 'M9.5 5.5 16 12l-6.5 6.5',
    display: 'M4 8h9M17 8h3M4 16h3M11 16h9M15 5.5v5M9 13.5v5',
    up: 'M5.5 14.5 12 8l6.5 6.5',
    down: 'M5.5 9.5 12 16l6.5-6.5',
    minus: 'M6 12h12',
    plus: 'M6 12h12M12 6v12',
    fit: 'M4 12h16M4 12l3-3M4 12l3 3M20 12l-3-3M20 12l-3 3',
    contents: 'M9 7h11M9 12h11M9 17h11M4.5 7h.01M4.5 12h.01M4.5 17h.01',
    bookmark: 'M7 4.5h10a.5.5 0 0 1 .5.5v14.5L12 15.5 6.5 19.5V5a.5.5 0 0 1 .5-.5Z',
    notes: 'M5 5.5h14v10H11l-4 3.5v-3.5H5Z',
    highlight: 'm14.5 5.5 4 4-8.5 8.5H6v-4ZM13 7l4 4M5 21h14',
    quote: 'M6 17v-4.5C6 9.5 7.5 7.5 10 7M14 17v-4.5c0-3 1.5-5 4-5.5M6 17h4v-4H6M14 17h4v-4h-4',
  };
</script>

<svg viewBox="0 0 24 24" width="1.15em" height="1.15em" fill={filled ? 'currentColor' : 'none'} stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">
  <path d={paths[name]} />
</svg>
