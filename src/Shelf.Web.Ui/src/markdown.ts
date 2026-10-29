import DOMPurify from 'dompurify';
import { marked } from 'marked';
import { assetUrl } from './api';

// A note is Markdown that a person wrote (or that came in through the API), so the HTML is cleaned before it is shown. What the
// content security policy blocks anyway (scripts, remote images) is removed here too, so that nothing in a note can run or load.
DOMPurify.addHook('afterSanitizeAttributes', (node) => {
  if (node.tagName === 'A') {
    node.setAttribute('rel', 'noopener noreferrer');
    node.setAttribute('target', '_blank');
  }
});

/**
 * The HTML of an article in Markdown (F-ART-8). Unlike a note it may show images, but only those the article saved next to itself
 * (`images/<name>`), served from the library; every other image is replaced by its description, so that nothing is loaded from the
 * site the article came from.
 */
export function renderArticle(text: string, fileId: string): string {
  const html = marked.parse(text, { async: false, gfm: true }) as string;
  const fragment = DOMPurify.sanitize(html, {
    USE_PROFILES: { html: true },
    FORBID_TAGS: ['style', 'form', 'input', 'textarea', 'button', 'select', 'svg', 'math'],
    FORBID_ATTR: ['style', 'srcset'],
    RETURN_DOM_FRAGMENT: true,
  });
  for (const image of Array.from(fragment.querySelectorAll('img'))) {
    const own = /^images\/([^/]+)$/.exec(image.getAttribute('src') ?? '');
    if (own) {
      image.setAttribute('src', assetUrl(fileId, own[1]));
      image.setAttribute('loading', 'lazy');
    } else {
      image.replaceWith(document.createTextNode(image.getAttribute('alt') ?? ''));
    }
  }
  const holder = document.createElement('div');
  holder.append(fragment);
  return holder.innerHTML;
}

/** The HTML of a note in Markdown, safe to insert into the page. */
export function renderMarkdown(text: string): string {
  const html = marked.parse(text, { async: false, gfm: true, breaks: true }) as string;
  return DOMPurify.sanitize(html, {
    USE_PROFILES: { html: true },
    FORBID_TAGS: ['style', 'form', 'input', 'textarea', 'button', 'select', 'img', 'svg', 'math'],
    FORBID_ATTR: ['style'],
  });
}
