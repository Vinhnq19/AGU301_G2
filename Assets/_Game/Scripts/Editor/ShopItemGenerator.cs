using System.IO;
using DungeonBuilder.Core.Enums;
using UnityEditor;
using UnityEngine;

namespace DungeonBuilder.Editor
{
    public class ShopItemGenerator
    {
        private struct ItemConfig
        {
            public string id;
            public string name;
            public int price;
            public int sell;
            public bool isUnlimited;
            public CurrencyType currencyType;
            public ResourceType resourceType;
            public int upgradeLevel; // 0 = không phải item nâng cấp theo level
        }

        [MenuItem("Dungeon Builder/Generate Shop Items")]
        public static void GenerateItems()
        {
            string savePath = "Assets/_Game/SO/ItemShop";
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            ItemConfig[] itemsToGenerate = new ItemConfig[]
            {
                new ItemConfig { id = "1", name = "Wood", price = 5, sell = 1, isUnlimited = true, currencyType = CurrencyType.Coin, resourceType = ResourceType.Wood },
                new ItemConfig { id = "2", name = "Stone", price = 5, sell = 1, isUnlimited = true, currencyType = CurrencyType.Coin, resourceType = ResourceType.Stone },
                new ItemConfig { id = "3", name = "Copper", price = 10, sell = 2, isUnlimited = true, currencyType = CurrencyType.Coin, resourceType = ResourceType.Copper },
                new ItemConfig { id = "4", name = "Iron", price = 15, sell = 3, isUnlimited = true, currencyType = CurrencyType.Coin, resourceType = ResourceType.Iron },
                new ItemConfig { id = "5", name = "Blue Gem", price = 30, sell = 6, isUnlimited = true, currencyType = CurrencyType.Coin, resourceType = ResourceType.BlueGems },
                new ItemConfig { id = "6", name = "Purple Gem", price = 50, sell = 10, isUnlimited = true, currencyType = CurrencyType.Coin, resourceType = ResourceType.PurpleGems },


                // Id 15 (KHÔNG dùng 7 — đã bị ShopItem_SpikeTrap chiếm; Id trùng làm giao dịch chạy sai item)
                new ItemConfig { id = "15", name = "Unlock Canon Tower", price = 100, sell = 0, isUnlimited = false, currencyType = CurrencyType.Token, resourceType = ResourceType.CannonTowerUnlock },
                new ItemConfig { id = "8", name = "Unlock Frost Tower", price = 120, sell = 0, isUnlimited = false, currencyType = CurrencyType.Token, resourceType = ResourceType.FrostTowerUnlock },
                new ItemConfig { id = "9", name = "Unlock Laze Tower", price = 200, sell = 0, isUnlimited = false, currencyType = CurrencyType.Token, resourceType = ResourceType.LaserTowerUnlock },


                // CẢNH BÁO: ResourceType = Coin nghĩa là "mua/bán tiền bằng tiền" — sell > 0 từng tạo
                // exploit in tiền (bán 1 Coin nhận 100 Coin). Giữ sell = 0 cho tới khi có ResourceType
                // HealPack thật; Shop.cs cũng đã chặn giao dịch với item có ResourceType là tiền tệ.
                new ItemConfig { id = "10", name = "Heal Pack", price = 500, sell = 0, isUnlimited = true, currencyType = CurrencyType.Coin, resourceType = ResourceType.Coin },

                new ItemConfig { id = "11", name = "Foraging Skill Upgrade Lv2", price = 3000, sell = 120, isUnlimited = false, currencyType = CurrencyType.Coin, resourceType = ResourceType.ForgingSkill, upgradeLevel = 2 },
                new ItemConfig { id = "12", name = "Mining Skill Upgrade Lv2", price = 3000, sell = 120, isUnlimited = false, currencyType = CurrencyType.Coin, resourceType = ResourceType.MiningSkill, upgradeLevel = 2 },


                new ItemConfig { id = "13", name = "Foraging Skill Upgrade Lv3", price = 6000, sell = 400, isUnlimited = false, currencyType = CurrencyType.Coin, resourceType = ResourceType.ForgingSkill, upgradeLevel = 3 },
                new ItemConfig { id = "14", name = "Mining Skill Upgrade Lv3", price = 6000, sell = 400, isUnlimited = false, currencyType = CurrencyType.Coin, resourceType = ResourceType.MiningSkill, upgradeLevel = 3 },
            };

            foreach (var config in itemsToGenerate)
            {
                ShopItem item = ScriptableObject.CreateInstance<ShopItem>();
                item.Id = config.id;
                item.Name = config.name;
                item.Price = config.price;
                item.Sell = config.sell;
                item.isUnlimited = config.isUnlimited;
                item.isSellable = config.sell > 0;
                item.CurrencyType = config.currencyType;
                item.RemainingQuantity = config.isUnlimited ? 0 : 1;
                item.ResourceType = config.resourceType;
                item.upgradeLevel = config.upgradeLevel;

                string fileName = $"ShopItem_{config.name.Replace(" ", "")}.asset";
                string fullPath = Path.Combine(savePath, fileName);

                AssetDatabase.CreateAsset(item, fullPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ShopItemGenerator] Successfully generated {itemsToGenerate.Length} shop items at {savePath}");
        }
    }
}
