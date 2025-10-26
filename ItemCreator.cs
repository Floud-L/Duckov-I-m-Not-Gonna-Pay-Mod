using ItemStatsSystem;
using SodaCraft.Localizations;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bank
{
    public static class ItemCreator
    {
        private static List<Item> createdItems = new List<Item>();

        public struct ItemInfo {
            public int OriginalItemTypeID;
            public int TypeID;
            public string LocalizationKey;
            public string DisplayName;
            public string DisplayDescription;
            public int Value;
            public float Weight;
            public int Quality;
            public DisplayQuality DisplayQuality;
        }

        public static void Init() {
            if (createdItems.Count == 0) {
                foreach (var itemInfo in itemInfos) {
                    var item = CreateItemAndRegister(itemInfo);
                    createdItems.Add(item);
                }
            }
            
        }

        public static void DestroyCreatedItems() {
            foreach (var item in createdItems) {
                Unregister(item);
                UnityEngine.Object.Destroy(item);
            }
            createdItems.Clear();
        }

        public static Item CreateItemAndRegister(ItemInfo itemInfo) {
            var originalItemPrefab = ItemAssetsCollection.GetPrefab(itemInfo.OriginalItemTypeID);
            if (originalItemPrefab == null) throw new ArgumentNullException(nameof(originalItemPrefab));
            var itemPrefab = UnityEngine.Object.Instantiate(originalItemPrefab);
            UnityEngine.Object.DontDestroyOnLoad(itemPrefab);
            SetItemField(itemPrefab, itemInfo);
            SetLocalization(itemInfo);
            Register(itemPrefab);


            return itemPrefab;
        }

        public static void SetItemField(Item item,ItemInfo itemInfo) {
            item.name = itemInfo.LocalizationKey;
            PrivateAccessor.TrySet(item, "typeID", itemInfo.TypeID);
            PrivateAccessor.TrySet(item, "weight", itemInfo.Weight);
            item.DisplayNameRaw = itemInfo.LocalizationKey;
            item.Value = itemInfo.Value;
            item.Quality= itemInfo.Quality;
            item.DisplayQuality = itemInfo.DisplayQuality;

        }

        public static void SetLocalization(ItemInfo itemInfo) {
            LocalizationManager.SetOverrideText(itemInfo.LocalizationKey, itemInfo.DisplayName);
            LocalizationManager.SetOverrideText(itemInfo.LocalizationKey+ "_Desc", itemInfo.DisplayDescription);
        }

        public static bool Register(Item item) {
            if (ItemAssetsCollection.AddDynamicEntry(item)) {
                Debug.Log($"[Bank] 物品[{item.DisplayName}]:[{item.DisplayNameRaw}]注册成功,ID:[{item.TypeID}] ");
                return true;
            }
            else { 
                Debug.LogWarning($"[Bank] 物品[{item.DisplayName}]:[{item.DisplayNameRaw}]注册失败,ID:[{item.TypeID}] ");
                return false;
            }
        }

        public static bool Unregister(Item item) {
            if (ItemAssetsCollection.RemoveDynamicEntry(item)) {
                Debug.Log($"[Bank] 物品[{item.DisplayName}]:[{item.DisplayNameRaw}]注销成功,ID:[{item.TypeID}] ");
                return true;
            }
            else {
                Debug.LogWarning($"[Bank] 物品[{item.DisplayName}]:[{item.DisplayNameRaw}]注销失败,ID:[{item.TypeID}] ");
                return false;
            }
        }

        private static List<ItemInfo> itemInfos = new List<ItemInfo>() {
            new ItemInfo {
                OriginalItemTypeID = 73,
                TypeID = ModBehaviour.DebtNoteTypeID,
                LocalizationKey = "Debt_Note",
                DisplayName = "欠条",
                DisplayDescription = "一张欠条:\"鸡年鸭月鹅日,借款XXX元.\"\n(将其带回基地会自动清除债务并销毁欠条)",
                Value = 10,
                Weight = 0.07f,
                Quality = 6,
                DisplayQuality = DisplayQuality.Orange,
            }
        };
    }
}
