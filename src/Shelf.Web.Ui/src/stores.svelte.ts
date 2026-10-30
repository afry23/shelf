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

/** The number of works per page that the library view starts with (`web.page_size`). Null until it is known. */
export const defaultPageSize = $state<{ value: number | null }>({ value: null });
const fallbackPageSize = 20;

/** The version of the running shelf, shown in the header. Null until it is known (or when the server refuses). */
export const version = $state<{ value: string | null }>({ value: null });

export async function refreshVersion(): Promise<void> {
  try {
    const current = await status();
    version.value = current.version;
    defaultPageSize.value = current.pageSize;
  } catch {
    version.value = null;
    defaultPageSize.value = fallbackPageSize;
  }
}
