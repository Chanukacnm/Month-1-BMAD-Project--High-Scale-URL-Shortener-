using System.Text;
using UrlShortener.Application.Common.Interfaces;

namespace UrlShortener.Application.Common.Services;

public class ShortCodeGenerator : IShortCodeGenerator
{
    private static readonly char[] Base62Chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();
    private readonly Random _random = new();

    public string Generate(int length = 7)
    {
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            sb.Append(Base62Chars[_random.Next(Base62Chars.Length)]);
        }
        return sb.ToString();
    }
}
