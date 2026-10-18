// The JSON API of `shelf serve` (/api/v1). Every answer that is not a success is a problem document.

export interface Problem {
  status: number;
  title: string;
  detail?: string;
}

export interface Status {
  version: string;
  works: number;
  files: number;
}

/** An error the server explained, or a request that did not get through. */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
  }
}

export async function get<T>(path: string): Promise<T> {
  const response = await fetch(`/api/v1${path}`, { headers: { Accept: 'application/json' } });
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as Problem | null;
    throw new ApiError(problem?.detail ?? problem?.title ?? `HTTP ${response.status}`, response.status);
  }
  return (await response.json()) as T;
}

export const status = () => get<Status>('/status');
