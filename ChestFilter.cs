using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemType = ItemDrop.ItemData.ItemType;

namespace AutoStash
{
    internal sealed class ItemCategory
    {
        private readonly string _english;

        private readonly string _russian;

        internal ItemCategory(string id, string english, string russian, Func<ItemDrop.ItemData.SharedData, bool> matches)
        {
            Id = id;
            _english = english;
            _russian = russian;
            Matches = matches;
        }

        internal string Id { get; }

        internal Func<ItemDrop.ItemData.SharedData, bool> Matches { get; }

        internal string Title => Text.T(_english, _russian);
    }

    // Every item belongs to exactly one category: the first one that matches.
    internal static class ItemCategories
    {
        internal static readonly ItemCategory[] All =
        {
            new ItemCategory("materials", "Materials", "Материалы", s => s.m_itemType == ItemType.Material),
            new ItemCategory("food", "Food", "Еда", s => s.m_itemType == ItemType.Consumable && (s.m_food > 0f || s.m_foodStamina > 0f || s.m_foodEitr > 0f)),
            new ItemCategory("potions", "Meads", "Зелья", s => s.m_itemType == ItemType.Consumable),
            new ItemCategory("trophies", "Trophies", "Трофеи", s => s.m_itemType == ItemType.Trophy),
            new ItemCategory("fish", "Fish", "Рыба", s => s.m_itemType == ItemType.Fish),
            new ItemCategory("weapons", "Weapons", "Оружие", s => Is(s, ItemType.OneHandedWeapon, ItemType.TwoHandedWeapon, ItemType.TwoHandedWeaponLeft, ItemType.Bow, ItemType.Attach_Atgeir)),
            new ItemCategory("armor", "Armor", "Броня", s => Is(s, ItemType.Helmet, ItemType.Chest, ItemType.Legs, ItemType.Hands, ItemType.Shoulder, ItemType.Shield, ItemType.Utility, ItemType.Trinket)),
            new ItemCategory("ammo", "Ammo", "Боеприпасы", s => Is(s, ItemType.Ammo, ItemType.AmmoNonEquipable)),
            new ItemCategory("tools", "Tools", "Инструменты", s => Is(s, ItemType.Tool, ItemType.Torch)),
            new ItemCategory("misc", "Other", "Прочее", s => true)
        };

        internal static ItemCategory Of(ItemDrop.ItemData.SharedData shared)
        {
            return shared == null ? null : All.First(c => c.Matches(shared));
        }

        internal static int IndexOf(ItemCategory category)
        {
            return Array.IndexOf(All, category);
        }

        internal static bool Exists(string id)
        {
            return All.Any(c => c.Id == id);
        }

        private static bool Is(ItemDrop.ItemData.SharedData shared, params ItemType[] types)
        {
            return types.Contains(shared.m_itemType);
        }
    }

    internal sealed class ChestFilter
    {
        internal const int RankPicked = 0;

        internal const int RankCategory = 1;

        internal const int RankNone = -1;

        private const int MaxItems = 512;

        private const int MaxNameLength = 128;

        internal readonly HashSet<string> Items = new HashSet<string>(StringComparer.Ordinal);

        internal readonly HashSet<string> Categories = new HashSet<string>(StringComparer.Ordinal);

        internal bool IsEmpty => Items.Count == 0 && Categories.Count == 0;

        internal int Count => Items.Count + Categories.Count;

        internal ChestFilter Copy()
        {
            ChestFilter copy = new ChestFilter();
            copy.Items.UnionWith(Items);
            copy.Categories.UnionWith(Categories);
            return copy;
        }

        // Items picked one by one win over items that are only accepted through a category.
        internal int Rank(ItemDrop.ItemData item)
        {
            string prefab = PrefabName(item);
            return Rank(prefab, item.m_shared);
        }

        internal int Rank(string prefab, ItemDrop.ItemData.SharedData shared)
        {
            if (prefab != null && Items.Contains(prefab))
            {
                return RankPicked;
            }
            ItemCategory category = ItemCategories.Of(shared);
            return category != null && Categories.Contains(category.Id) ? RankCategory : RankNone;
        }

        internal static string PrefabName(ItemDrop.ItemData item)
        {
            return item != null && item.m_dropPrefab ? item.m_dropPrefab.name : null;
        }

        internal static bool ValidName(string name)
        {
            return !string.IsNullOrEmpty(name) && name.Length <= MaxNameLength && name.IndexOf(',') < 0 && name.IndexOf('|') < 0 && !name.Any(char.IsControl);
        }

        // Format: "category,category|Prefab,Prefab". An empty string means the chest accepts nothing.
        internal string Encode()
        {
            if (Items.Count > MaxItems || !Items.All(ValidName) || !Categories.All(ItemCategories.Exists))
            {
                throw new InvalidOperationException(Text.T("The filter has too many or invalid entries.", "В фильтре слишком много или некорректные записи."));
            }
            if (IsEmpty)
            {
                return "";
            }
            return string.Join(",", Categories.OrderBy(c => c, StringComparer.Ordinal)) + "|" + string.Join(",", Items.OrderBy(i => i, StringComparer.Ordinal));
        }

        internal static ChestFilter Decode(string raw)
        {
            ChestFilter filter = new ChestFilter();
            if (string.IsNullOrEmpty(raw))
            {
                return filter;
            }
            int split = raw.IndexOf('|');
            string categories = split >= 0 ? raw.Substring(0, split) : raw;
            string items = split >= 0 ? raw.Substring(split + 1) : "";
            foreach (string category in categories.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (ItemCategories.Exists(category))
                {
                    filter.Categories.Add(category);
                }
            }
            foreach (string item in items.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (ValidName(item) && filter.Items.Count < MaxItems)
                {
                    filter.Items.Add(item);
                }
            }
            return filter;
        }
    }

    internal static class FilterStore
    {
        internal const string ZdoKey = "AutoStash.Filter.v1";

        private static string _lastRaw;

        private static ChestFilter _lastFilter;

        // Player-built storage only: chests, carts and ships. Tombstones and loot bags have no Piece.
        internal static bool Eligible(Container chest)
        {
            return chest
                && chest.GetInventory() != null
                && chest.GetComponentInParent<Piece>()
                && !chest.GetComponent<TombStone>()
                && !chest.m_autoDestroyEmpty;
        }

        internal static ZNetView NetView(Container chest)
        {
            return chest ? GameAccess.NetView(chest) : null;
        }

        internal static string Raw(Container chest)
        {
            ZNetView view = NetView(chest);
            return view && view.IsValid() ? view.GetZDO().GetString(ZdoKey, "") : "";
        }

        internal static ChestFilter Load(Container chest)
        {
            string raw = Raw(chest);
            if (raw != _lastRaw)
            {
                _lastFilter = ChestFilter.Decode(raw);
                _lastRaw = raw;
            }
            return _lastFilter;
        }

        // Opening a chest makes the local player the owner of its ZDO, so only a player who could open
        // the chest (ward and privacy checks passed) can change its filter.
        internal static bool CanEdit(Container chest, out string reason)
        {
            Player player = Player.m_localPlayer;
            ZNetView view = NetView(chest);
            if (!Eligible(chest) || !player || player.IsDead() || GameAccess.OpenedChest() != chest)
            {
                reason = Text.T("Open the chest to edit its filter.", "Откройте сундук, чтобы настроить фильтр.");
                return false;
            }
            if (Vector3.Distance(player.transform.position, chest.transform.position) > 5f)
            {
                reason = Text.T("Move closer to the chest.", "Подойдите ближе к сундуку.");
                return false;
            }
            if (!view || !view.IsValid() || !view.IsOwner())
            {
                reason = Text.T("The chest is busy. Reopen it and try again.", "Сундук занят. Откройте его заново и попробуйте ещё раз.");
                return false;
            }
            reason = "";
            return true;
        }

        internal static bool Save(Container chest, ChestFilter filter, string expected, out string message)
        {
            if (!CanEdit(chest, out message))
            {
                return false;
            }
            if (Raw(chest) != expected)
            {
                message = Text.T("Someone else changed this filter. Reopen the chest to see it.", "Кто-то уже изменил этот фильтр. Откройте сундук заново.");
                return false;
            }
            try
            {
                NetView(chest).GetZDO().Set(ZdoKey, filter.Encode());
                message = "";
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }
    }
}
