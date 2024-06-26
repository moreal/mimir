using System.Security.Cryptography;
using Bencodex;
using Bencodex.Types;
using Libplanet.Common;
using MongoDB.Bson;
using Nekoyume.Model.Elemental;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;

namespace Mimir.Models.Assets;

public class Item
{
    private static readonly Codec Codec = new();

    public int ItemSheetId { get; set; }
    public int Grade { get; set; }
    public ItemType ItemType { get; set; }
    public ItemSubType ItemSubType { get; set; }
    public ElementalType ElementalType { get; set; }
    public int Count { get; set; }

    public int? Level { get; set; }
    public long? RequiredBlockIndex { get; set; }
    public HashDigest<SHA256>? FungibleId { get; set; }
    public Guid? NonFungibleId { get; set; }
    public Guid? TradableId { get; set; }

    public Item(ItemBase itemBase, int count) => Reset(itemBase, count);

    public Item(Nekoyume.Model.Item.Inventory.Item inventoryItem) : this(inventoryItem.item, inventoryItem.count)
    {
    }

    public Item(BsonDocument inventoryItem)
    {
        var item = inventoryItem["item"].AsBsonDocument;
        if (item.Contains("serialized"))
        {
            var serialized = item["serialized"].AsString;
            var base64Encoded = Convert.FromBase64String(serialized);
            var serializedDictionary = (Dictionary)Codec.Decode(base64Encoded);
            var itemType = serializedDictionary["item_type"].ToEnum<ItemType>();
            ItemBase itemBase = itemType switch
            {
                ItemType.Consumable => new Consumable(serializedDictionary),
                ItemType.Costume => new Costume(serializedDictionary),
                ItemType.Equipment => new Equipment(serializedDictionary),
                ItemType.Material => new Material(serializedDictionary),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(itemType),
                    $"Invalid ItemType: {itemType}")
            };
            Reset(itemBase, inventoryItem["count"].AsInt32);
            return;
        }

        ItemSheetId = item["Id"].AsInt32;
        Grade = item["Grade"].AsInt32;
        ItemType = (ItemType)item["ItemType"].AsInt32;
        ItemSubType = (ItemSubType)item["ItemSubType"].AsInt32;
        ElementalType = (ElementalType)item["ElementalType"].AsInt32;
        Count = inventoryItem["count"].AsInt32;

        Level = item.Contains("level")
            ? item["level"].AsInt32
            : null;
        RequiredBlockIndex = item.Contains("RequiredBlockIndex")
            ? item["RequiredBlockIndex"].ToInt64()
            : null;

        try
        {
            FungibleId = item.Contains("FungibleId")
                ? HashDigest<SHA256>.FromString(item["FungibleId"].AsString)
                : null;
        }
        catch
        {
            FungibleId = null;
        }

        NonFungibleId = item.Contains("NonFungibleId")
            ? Guid.TryParse(item["NonFungibleId"].AsString, out var nfi)
                ? nfi
                : null
            : null;
        TradableId = item.Contains("TradableId")
            ? Guid.TryParse(item["TradableId"].AsString, out var ti)
                ? ti
                : null
            : null;
    }

    private void Reset(ItemBase itemBase, int count)
    {
        ItemSheetId = itemBase.Id;
        Grade = itemBase.Grade;
        ItemType = itemBase.ItemType;
        ItemSubType = itemBase.ItemSubType;
        ElementalType = itemBase.ElementalType;
        Count = count;

        Level = itemBase switch
        {
            Equipment e => e.level,
            _ => null
        };
        RequiredBlockIndex = itemBase switch
        {
            INonFungibleItem nfi => nfi.RequiredBlockIndex,
            ITradableItem ti => ti.RequiredBlockIndex,
            _ => null
        };
        FungibleId = itemBase is IFungibleItem fungibleItem
            ? fungibleItem.FungibleId
            : null;
        NonFungibleId = itemBase is INonFungibleItem nonFungibleItem
            ? nonFungibleItem.NonFungibleId
            : null;
        TradableId = itemBase is ITradableItem tradableItem
            ? tradableItem.TradableId
            : null;
    }
}
