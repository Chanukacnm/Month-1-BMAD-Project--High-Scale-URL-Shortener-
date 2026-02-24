using UrlShortener.Application.Common.Services;

namespace UrlShortener.Tests;

public class ShortCodeGeneratorTests
{
    private readonly ShortCodeGenerator _generator = new();

    [Fact]
    public void Generate_ReturnsCorrectDefaultLength()
    {
        var code = _generator.Generate();
        Assert.Equal(7, code.Length);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(10)]
    public void Generate_ReturnsRequestedLength(int length)
    {
        var code = _generator.Generate(length);
        Assert.Equal(length, code.Length);
    }

    [Fact]
    public void Generate_UsesOnlyBase62Characters()
    {
        const string base62 = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        for (int i = 0; i < 50; i++)
        {
            var code = _generator.Generate();
            foreach (var c in code)
            {
                Assert.Contains(c, base62);
            }
        }
    }

    [Fact]
    public void Generate_ProducesUniqueCodesAcrossMultipleCalls()
    {
        var codes = new HashSet<string>();

        for (int i = 0; i < 1000; i++)
        {
            codes.Add(_generator.Generate());
        }

        // With 62^7 possible codes, 1000 generations should never collide
        Assert.Equal(1000, codes.Count);
    }

    [Fact]
    public void Generate_IsThreadSafe()
    {
        // Verify that Random.Shared doesn't throw under concurrent access
        var codes = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, 100, _ =>
        {
            codes.Add(_generator.Generate());
        });

        Assert.Equal(100, codes.Count);
        Assert.All(codes, code => Assert.Equal(7, code.Length));
    }
}
