namespace EventsWorld.IntegrationTests.Infrastructure;

/// <summary>Paths inside the repository, found from the test output folder.</summary>
internal static class SolutionPaths
{
    public static string Root { get; } = FindRoot();

    public static string ApiProject => Path.Combine(Root, "src", "Bootstrapper", "EventsWorld.Api");

    public static string File(params string[] parts) => Path.Combine([Root, .. parts]);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !System.IO.File.Exists(Path.Combine(directory.FullName, "EventsWorld.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate 'EventsWorld.slnx' from the test output folder.");
    }
}
