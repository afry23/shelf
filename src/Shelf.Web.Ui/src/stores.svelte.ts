import { duplicates, status } from './api';

/** The number of groups of duplicates, shown in the header. Null until it is known (or when the server refuses). */
export const duplicateCount = $state<{ value: number | null }>({ value: null });

export async function refreshDuplicateCount(): Promise<void> {
  try {
    duplicateCount.value = (await duplicates()).total;
  } catch {
    duplicateCount.value = null;
  }
}

/** The version of the running shelf, shown in the header. Null until it is known (or when the server refuses). */
export const version = $state<{ value: string | null }>({ value: null });

export async function refreshVersion(): Promise<void> {
  try {
    version.value = (await status()).version;
  } catch {
    version.value = null;
  }
}
