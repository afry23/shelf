using Reqnroll;
using Shelf.Index;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>Steps for <c>shelf import --from</c>: a fetcher that copies fixtures instead of starting rclone.</summary>
[Binding]
public sealed class RemoteSteps(ScenarioEnvironment environment)
{
    [Given("the rclone remote {string} holds the fixtures {string}")]
    public void GivenTheRemoteHoldsTheFixtures(string remote, string fixtures) =>
        environment.Remote.Contents[remote] = [.. fixtures.Split(", ")];

    [Given("rclone is not installed")]
    public void GivenRcloneIsNotInstalled() => environment.Remote.Missing = true;

    [Given("the rclone call fails with {string}")]
    public void GivenTheRcloneCallFailsWith(string message) => environment.Remote.FailWith = message;

    [Then("rclone was called with the arguments {string}")]
    public void ThenRcloneWasCalledWith(string arguments) =>
        Assert.Contains(environment.Remote.Calls, call => call.Contains(arguments, StringComparison.Ordinal));

    [Then("rclone was not called")]
    public void ThenRcloneWasNotCalled() => Assert.Empty(environment.Remote.Calls);

    [Then("no rclone download folder is left behind")]
    public void ThenNoDownloadFolderIsLeftBehind()
    {
        Assert.NotEmpty(environment.Remote.Destinations);
        Assert.All(environment.Remote.Destinations, destination => Assert.False(Directory.Exists(destination), destination));
    }
}

/// <summary>Serves fixtures per remote and records every call; never starts a process.</summary>
public sealed class FakeRemote : IRemoteFetcher
{
    public Dictionary<string, List<string>> Contents { get; } = new();

    public bool Missing { get; set; }

    public string? FailWith { get; set; }

    /// <summary>The command line of every call, as the real fetcher would build it.</summary>
    public List<string> Calls { get; } = [];

    public List<string> Destinations { get; } = [];

    public void Fetch(string remote, string destination, CancellationToken cancellation)
    {
        Destinations.Add(destination);
        if (Missing)
        {
            throw new RemoteFetchException("rclone was not found; install it from https://rclone.org", toolMissing: true);
        }

        Calls.Add(string.Join(' ', RcloneFetcher.Arguments(remote, destination)));
        if (FailWith is not null)
        {
            throw new RemoteFetchException($"rclone failed (exit code 1): {FailWith}");
        }

        foreach (var fixture in Contents.GetValueOrDefault(remote) ?? [])
        {
            File.Copy(CoverSteps.FixturePath(fixture), Path.Combine(destination, Path.GetFileName(fixture)));
        }
    }
}
