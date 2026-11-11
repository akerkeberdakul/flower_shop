using FlowerShop;

namespace FlowerShop.Tests;

public class DataPathsTests
{
    [Fact]
    public void Resolve_RootedPath_IsReturnedUnchanged()
    {
        var rooted = Path.Combine(Path.GetTempPath(), "some_file.csv");

        Assert.Equal(rooted, DataPaths.Resolve(rooted));
    }

    [Fact]
    public void Resolve_FileThatExistsNowhere_IsReturnedUnchanged()
    {
        var name = "no_such_file_" + Guid.NewGuid().ToString("N") + ".csv";

        Assert.Equal(name, DataPaths.Resolve(name));
    }
}
