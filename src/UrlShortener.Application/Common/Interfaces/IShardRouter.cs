namespace UrlShortener.Application.Common.Interfaces;

public interface IShardRouter
{
    int GetShardIndex(string shortCode);
}
