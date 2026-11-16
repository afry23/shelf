using System.ComponentModel;
using System.Diagnostics;
using Shelf.Core.Configuration;
using Shelf.Core.Model;

namespace Shelf.Index;

/// <summary>Why a conversion did not happen: <see cref="Usage"/> is a request that cannot be met as given, <see cref="Failed"/> a run that went wrong.</summary>
public enum ConvertFailureKind
{
    Usage,
    Failed,
}

/// <summary>A conversion that was refused or that failed (F-CNV-1 to F-CNV-3); the message is meant to be shown.</summary>
public sealed class ConvertException(string message, ConvertFailureKind kind, Exception? innerException = null) : Exception(message, innerException)
{
    public ConvertFailureKind Kind { get; } = kind;
}

/// <summary>Runs the converter of <c>convert.command</c> (F-CNV-2).</summary>
public interface IPdfConverter
{
    /// <param name="command">The template with <c>{input}</c> and <c>{output}</c>.</param>
    /// <param name="input">The PDF.</param>
    /// <param name="output">The EPUB the converter has to write.</param>
    /// <exception cref="ConvertException">The tool is missing, failed or ran too long.</exception>
    void Convert(string command, string input, string output, TimeSpan timeout, CancellationToken cancellation);
}

/// <summary>Starts the user's own converter as a process, without a shell; shelf holds no converter and goes nowhere online.</summary>
public sealed class ProcessConverter : IPdfConverter
{
    /// <summary>
    /// The program and its arguments: the template is split at spaces first, then <c>{input}</c> and <c>{output}</c> are
    /// replaced, so a path with spaces stays one argument and nothing is parsed by a shell.
    /// </summary>
    public static IReadOnlyList<string> Arguments(string command, string input, string output) =>
        [.. command.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Replace("{input}", input, StringComparison.Ordinal).Replace("{output}", output, StringComparison.Ordinal))];

    public void Convert(string command, string input, string output, TimeSpan timeout, CancellationToken cancellation)
    {
        var parts = Arguments(command, input, output);
        var info = new ProcessStartInfo(parts[0]) { RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in parts.Skip(1))
        {
            info.ArgumentList.Add(argument);
        }

        Process process;
        try
        {
            process = Process.Start(info)!;
        }
        catch (Win32Exception e)
        {
            throw new ConvertException($"{parts[0]} was not found; install it or change convert.command", ConvertFailureKind.Usage, e);
        }

        using (process)
        {
            using var registration = cancellation.Register(() => process.Kill(entireProcessTree: true));
            _ = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeout))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                throw new ConvertException($"{parts[0]} timed out after {(int)timeout.TotalSeconds} s", ConvertFailureKind.Failed);
            }

            cancellation.ThrowIfCancellationRequested();
            if (process.ExitCode != 0)
            {
                var detail = error.GetAwaiter().GetResult().Trim().ReplaceLineEndings(" ");
                throw new ConvertException($"{parts[0]} failed (exit code {process.ExitCode}): {detail}", ConvertFailureKind.Failed);
            }
        }
    }
}

/// <param name="File">The PDF to convert; null takes the only PDF of the work.</param>
/// <param name="Replace">An existing EPUB of the work is replaced (it goes to the trash).</param>
public sealed record ConvertOptions(string? File = null, bool Replace = false);

/// <summary>What <see cref="LibraryConvert.Convert"/> did.</summary>
public sealed record ConvertResult(Ulid Work, string Source, string File, string? Replaced);

/// <summary>PDF to EPUB with a converter of the person's own (F-CNV-1 to F-CNV-3).</summary>
public static class LibraryConvert
{
    /// <exception cref="ConvertException"/>
    /// <exception cref="LibraryEditException"/>
    public static ConvertResult Convert(
        string root,
        LibraryWork work,
        ConvertOptions options,
        ConvertSettings settings,
        IPdfConverter converter,
        string workingDirectory,
        CancellationToken cancellation)
    {
        if (settings.Command is null)
        {
            throw new ConvertException("no converter is configured; set convert.command in the config, e.g. command = \"pdf2epub {input} {output}\"", ConvertFailureKind.Usage);
        }

        var sidecar = LibraryEditor.Read(root, work);
        var books = sidecar.Files.Where(f => f.Role == FileRole.Book).ToList();
        var pdf = PickPdf(root, work, sidecar, books, options, workingDirectory);
        var existing = books.FirstOrDefault(f => f.Name.EndsWith(".epub", StringComparison.OrdinalIgnoreCase));
        if (existing is not null && !options.Replace)
        {
            throw new ConvertException($"{work.Id} already has an EPUB ({existing.Name}); use --replace to convert again", ConvertFailureKind.Usage);
        }

        var pdfPath = LibraryEditor.FilePath(root, work, pdf.Name);
        if (FileAnalyzer.Analyze(pdfPath).TextStatus == "none")
        {
            throw new ConvertException($"{pdf.Name} has no text layer; shelf has no OCR, so it cannot be converted", ConvertFailureKind.Usage);
        }

        var temporary = Directory.CreateTempSubdirectory("shelf-convert-").FullName;
        try
        {
            var output = Path.Combine(temporary, Path.ChangeExtension(pdf.Name, ".epub"));
            converter.Convert(settings.Command, pdfPath, output, TimeSpan.FromSeconds(settings.TimeoutSeconds), cancellation);
            Check(output, settings.Command);
            if (existing is not null && FileAnalyzer.Analyze(LibraryEditor.FilePath(root, work, existing.Name)).Sha256 == FileAnalyzer.Analyze(output).Sha256)
            {
                throw new ConvertException($"the converter made an EPUB that is identical to {existing.Name}; nothing changed", ConvertFailureKind.Usage);
            }

            var added = LibraryFiles.AddFile(root, work, output, new AddOptions(Replace: existing?.Name), workingDirectory);
            return new ConvertResult(work.Id, pdf.Name, added.File, added.Replaced);
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static SidecarFile PickPdf(string root, LibraryWork work, WorkSidecar sidecar, List<SidecarFile> books, ConvertOptions options, string workingDirectory)
    {
        var pdfs = books.Where(f => f.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
        if (options.File is { } name)
        {
            var found = LibraryEditor.FindFile(root, work, sidecar, name, workingDirectory)
                ?? throw new ConvertException($"{work.Id} has no file '{name}'", ConvertFailureKind.Usage);
            return pdfs.FirstOrDefault(f => f.Name == found.Name)
                ?? throw new ConvertException($"{found.Name} is no PDF; only a PDF can be converted", ConvertFailureKind.Usage);
        }

        return pdfs.Count switch
        {
            0 => throw new ConvertException($"{work.Id} has no PDF to convert", ConvertFailureKind.Usage),
            1 => pdfs[0],
            _ => throw new ConvertException($"{work.Id} has {pdfs.Count} PDF files; name one with --file ({string.Join(", ", pdfs.Select(f => f.Name))})", ConvertFailureKind.Usage),
        };
    }

    /// <summary>The result must be an EPUB that can be read and has text; else nothing is added (F-CNV-2).</summary>
    private static void Check(string output, string command)
    {
        var tool = ProcessConverter.Arguments(command, string.Empty, string.Empty)[0];
        if (!File.Exists(output))
        {
            throw new ConvertException($"{tool} wrote no output file", ConvertFailureKind.Failed);
        }

        var analysis = FileAnalyzer.Analyze(output);
        if (analysis.Error is not null)
        {
            throw new ConvertException($"the result of {tool} is not a valid EPUB: {analysis.Error.Message}", ConvertFailureKind.Failed);
        }

        if (analysis.Chunks.Count == 0)
        {
            throw new ConvertException($"the result of {tool} has no chapter text", ConvertFailureKind.Failed);
        }
    }
}
