namespace Shelf.Specs.Steps;

/// <summary>
/// Per-scenario environment variables shared by step classes. Never reads or writes the process environment.
/// </summary>
public sealed class ScenarioEnvironment
{
    public Dictionary<string, string> Variables { get; } = new();
}
