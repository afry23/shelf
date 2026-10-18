import { duplicates } from './api';

/** The number of groups of duplicates, shown in the header. Null until it is known (or when the server refuses). */
export const duplicateCount = $state<{ value: number | null }>({ value: null });

export async function refreshDuplicateCount(): Promise<void> {
  try {
    duplicateCount.value = (await duplicates()).total;
  } catch {
    duplicateCount.value = null;
  }
}
