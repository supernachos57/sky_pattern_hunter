namespace SkyPatternHunter.SmokeTests.Presentation;

public class MainWindowTests
{
    [Fact]
    public async Task MainWindow_LabelsSpeedAndMphColumns()
    {
        var repositoryRoot = FindRepositoryRoot();
        var xamlPath = Path.Combine(repositoryRoot, "src", "SkyPatternHunter.Presentation", "MainWindow.xaml");

        var xaml = await File.ReadAllTextAsync(xamlPath);

        Assert.Equal(2, CountOccurrences(xaml, "Header=\"Speed\""));
        Assert.Equal(2, CountOccurrences(xaml, "Header=\"MPH\""));
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "SkyPatternHunter.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static int CountOccurrences(string value, string match)
    {
        var count = 0;
        var startIndex = 0;

        while ((startIndex = value.IndexOf(match, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += match.Length;
        }

        return count;
    }
}
