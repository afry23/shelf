<script lang="ts">
  import { ApiError, status, type Status } from './api';

  let info = $state<Status | null>(null);
  let error = $state<ApiError | Error | null>(null);

  $effect(() => {
    status()
      .then((s) => (info = s))
      .catch((e: Error) => (error = e));
  });
</script>

<main>
  <h1>shelf</h1>
  {#if info}
    <p>
      Version {info.version}: {info.works} {info.works === 1 ? 'work' : 'works'} with {info.files}
      {info.files === 1 ? 'file' : 'files'}.
    </p>
  {:else if error instanceof ApiError && error.status === 401}
    <p>
      This server needs an access token. Open the address that <code>shelf serve</code> was started for with
      <code>?token=…</code> once; the browser keeps the token after that.
    </p>
  {:else if error}
    <p role="alert">Could not reach the library: {error.message}</p>
  {:else}
    <p>Loading…</p>
  {/if}
</main>
