import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import type { Plugin } from 'vite';
import { defineConfig } from 'vite';
import { svelte } from '@sveltejs/vite-plugin-svelte';

// PDF.js fetches fonts, character maps, color profiles and WebAssembly decoders at run time. They are part of the build (embedded
// in the binary like everything else; the reader needs no network) and are served from /pdfjs/ in development too.
const pdfjsRoot = join('node_modules', 'pdfjs-dist');
const pdfjsFolders = ['standard_fonts', 'cmaps', 'iccs', 'wasm'];

function files(folder: string): string[] {
  return readdirSync(folder).flatMap((name) => {
    const path = join(folder, name);
    return statSync(path).isDirectory() ? files(path) : [path];
  });
}

function pdfjsAssets(): Plugin {
  return {
    name: 'shelf-pdfjs-assets',
    generateBundle() {
      for (const folder of pdfjsFolders) {
        for (const path of files(join(pdfjsRoot, folder))) {
          this.emitFile({ type: 'asset', fileName: `pdfjs/${relative(pdfjsRoot, path).replaceAll('\\', '/')}`, source: readFileSync(path) });
        }
      }
    },
    configureServer(server) {
      server.middlewares.use('/pdfjs', (request, response, next) => {
        const name = decodeURIComponent((request.url ?? '').split('?')[0]).replace(/^\/+/, '');
        const allowed = pdfjsFolders.some((f) => name.startsWith(`${f}/`)) && !name.includes('..');
        if (!allowed) {
          return next();
        }
        try {
          response.setHeader('Content-Type', name.endsWith('.wasm') ? 'application/wasm' : 'application/octet-stream');
          response.end(readFileSync(join(pdfjsRoot, name)));
        } catch {
          next();
        }
      });
    },
  };
}

// `npm run dev` serves the UI with hot reload and sends /api to a running `shelf serve`.
export default defineConfig({
  plugins: [svelte(), pdfjsAssets()],
  build: { outDir: 'dist', emptyOutDir: true },
  server: { proxy: { '/api': 'http://127.0.0.1:8420' } },
});
