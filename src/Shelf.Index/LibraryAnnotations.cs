using Shelf.Core.Model;

namespace Shelf.Index;

/// <summary>
/// The highlights and notes of a work (F-ANN-1 to F-ANN-3). <c>annotations.json</c> next to <c>book.json</c> is the
/// source of truth; the index holds a copy for search. Like reading progress, a change writes the file and updates the
/// one work in the index without scanning the library, and it is not in the journal (ADR 0018).
/// </summary>
public static class LibraryAnnotations
{
    public static string PathOf(string root, LibraryWork work) => Path.Combine(root, work.Directory, AnnotationsFormat.FileName);

    /// <exception cref="SidecarException">The file is invalid.</exception>
    public static IReadOnlyList<Annotation> List(string root, LibraryWork work) => AnnotationsStore.Read(PathOf(root, work)).Annotations;

    /// <summary>Adds the annotation and mirrors the work in the index.</summary>
    /// <exception cref="SidecarException">The file or the annotation is invalid.</exception>
    /// <exception cref="IndexException"/>
    public static void Add(string root, LibraryWork work, Annotation annotation) =>
        Change(root, work, current => [.. current, annotation]);

    /// <summary>Replaces the annotation with this ID by the changed one; false if there is none.</summary>
    /// <exception cref="SidecarException"/>
    /// <exception cref="IndexException"/>
    public static bool Update(string root, LibraryWork work, Ulid id, Func<Annotation, Annotation> change)
    {
        if (!List(root, work).Any(a => a.Id == id))
        {
            return false;
        }

        var found = false;
        Change(root, work, current => [.. current.Select(a =>
        {
            if (a.Id != id)
            {
                return a;
            }

            found = true;
            return change(a);
        })]);
        return found;
    }

    /// <summary>Removes the annotation with this ID; false if there is none.</summary>
    /// <exception cref="SidecarException"/>
    /// <exception cref="IndexException"/>
    public static bool Delete(string root, LibraryWork work, Ulid id)
    {
        if (!List(root, work).Any(a => a.Id == id))
        {
            return false; // nothing to write: the file stays as it is
        }

        var found = false;
        Change(root, work, current =>
        {
            var rest = current.Where(a => a.Id != id).ToList();
            found = rest.Count != current.Count;
            return rest;
        });
        return found;
    }

    /// <summary>
    /// Moves the annotations of the named files of <paramref name="source"/> to <paramref name="target"/>, under the new
    /// file names, as a merge or a split moves the files. Annotations of other files stay. Does not touch the index; the
    /// scan that follows a merge or a split mirrors both works.
    /// </summary>
    internal static void Carry(string root, LibraryWork source, LibraryWork target, IReadOnlyDictionary<string, string> renames)
    {
        var sourcePath = PathOf(root, source);
        AnnotationsSidecar outgoing;
        try
        {
            outgoing = AnnotationsStore.Read(sourcePath);
        }
        catch (SidecarException)
        {
            return; // an annotations.json that cannot be read stays where it is
        }

        var moving = outgoing.Annotations.Where(a => renames.ContainsKey(a.File)).ToList();
        if (moving.Count == 0)
        {
            return;
        }

        var targetPath = PathOf(root, target);
        var into = AnnotationsStore.Read(targetPath);
        var carried = moving.Select(a => a with { File = renames[a.File] }).ToList();
        var staying = outgoing.Annotations.Where(a => !renames.ContainsKey(a.File)).ToList();
        AnnotationsStore.Write(targetPath, into with { Annotations = [.. into.Annotations, .. carried] });
        AnnotationsStore.Write(sourcePath, outgoing with { Annotations = staying });
    }

    /// <summary>The annotations of the work folder, for the copy in the index; a file that cannot be read is a problem and has none.</summary>
    internal static IReadOnlyList<Annotation> ReadForMirror(string root, string directory, List<ReindexProblem> problems)
    {
        var path = Path.Combine(root, directory, AnnotationsFormat.FileName);
        try
        {
            return AnnotationsStore.Read(path).Annotations;
        }
        catch (SidecarException e)
        {
            problems.Add(new ReindexProblem(path, e.Message));
            return [];
        }
    }

    private static void Change(string root, LibraryWork work, Func<IReadOnlyList<Annotation>, IReadOnlyList<Annotation>> change)
    {
        var path = PathOf(root, work);
        var current = AnnotationsStore.Read(path);
        var changed = current with { Annotations = change(current.Annotations) };
        AnnotationsStore.Write(path, changed);

        using var connection = IndexDatabase.OpenWrite(IndexDatabase.PathFor(root));
        using var writer = new IndexWriter(connection);
        using var transaction = writer.BeginTransaction();
        writer.ReplaceAnnotations(work.Id.ToString(), changed.Annotations);
        transaction.Commit();
    }
}
