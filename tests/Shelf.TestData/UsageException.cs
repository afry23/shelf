namespace Shelf.TestData;

/// <summary>Invalid arguments of the generator CLI; reported with exit code 2.</summary>
public sealed class UsageException(string message) : Exception(message);
