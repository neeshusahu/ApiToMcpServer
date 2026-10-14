using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;

[TestFixture]
public class RegistryReaderTests
{
    private string _dir = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Directory.CreateTempSubdirectory("registry-").FullName;
        File.WriteAllText(Path.Combine(_dir, "registry.json"), """
            { "baseUrl": "http://localhost:5080", "operations": {} }
            """);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_dir, recursive: true);

    private RegistryReader Create(string path)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ApiRegistry:Path"] = path })
            .Build();
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.ContentRootPath).Returns(_dir);
        return new RegistryReader(configuration, environment.Object);
    }

    [Test]
    public void RelativePath_IsResolvedFromContentRoot()
    {
        Assert.That(Create("registry.json").registry.BaseUrl, Is.EqualTo("http://localhost:5080"));
    }

    [Test]
    public void AbsolutePath_IsUsedAsIs()
    {
        Assert.That(Create(Path.Combine(_dir, "registry.json")).registry.BaseUrl, Is.EqualTo("http://localhost:5080"));
    }

    [Test]
    public void MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => Create("missing.json"));
    }
}
