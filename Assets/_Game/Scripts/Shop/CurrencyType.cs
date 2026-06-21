using DungeonBuilder.Core.Enums;

public enum CurrencyType
{
    Coin,
    Token,
}

/// <summary>Maps a shop currency to its tracked ResourceType.</summary>
public static class CurrencyTypeExtensions
{
    public static ResourceType ToResourceType(this CurrencyType currency) =>
        currency == CurrencyType.Token ? ResourceType.Token : ResourceType.Coin;
}
