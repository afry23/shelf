import DOMPurify from 'dompurify';
import { marked } from 'marked';

// A note is Markdown that a person wrote (or that came in through the API), so the HTML is cleaned before it is shown. What the
// content security policy blocks anyway (scripts, remote images) is removed here too, so that nothing in a note can run or load.
DOMPurify.addHook('afterSanitizeAttributes', (node) => {
  if (node.tagName === 'A') {
    node.setAttribute('rel', 'noopener noreferrer');
    node.setAttribute('target', '_blank');
  }
});

/** The HTML of a note in Markdown, safe to insert into the page. */
export function renderMarkdown(text: string): string {
  const html = marked.parse(text, { async: false, gfm: true, breaks: true }) as string;
  return DOMPurify.sanitize(html, {
    USE_PROFILES: { html: true },
    FORBID_TAGS: ['style', 'form', 'input', 'textarea', 'button', 'select', 'img', 'svg', 'math'],
    FORBID_ATTR: ['style'],
  });
}
