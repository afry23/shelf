/// <reference types="svelte" />
/// <reference types="vite/client" />

// foliate-js ships no type definitions; the reader uses the parts of it that its README documents.
declare module 'foliate-js/view.js';
declare module '*.mjs?url' {
  const url: string;
  export default url;
}
declare module 'foliate-js/overlayer.js';
