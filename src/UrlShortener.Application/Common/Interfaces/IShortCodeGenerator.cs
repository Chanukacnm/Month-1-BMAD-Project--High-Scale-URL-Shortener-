namespace UrlShortener.Application.Common.Interfaces;

public interface IShortCodeGenerator
{
    string Generate(int length = 7);
}
