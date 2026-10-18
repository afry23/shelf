/** The ID of a file is the ID of its work, a tilde and its name in the sidecar (`01J9...~kp.epub`). */
export const FileIds = {
  split(id: string): { work: string; name: string } | null {
    const at = id.indexOf('~');
    return at < 1 || at === id.length - 1 ? null : { work: id.slice(0, at), name: id.slice(at + 1) };
  },
};
