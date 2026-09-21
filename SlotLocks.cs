using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace AutoStash
{
    // Locked slots belong to the character: they are kept in the player's custom data and saved with the
    // character, so every world sees the same locks and removing the mod leaves nothing behind.
    internal static class SlotLocks
    {
        private const string DataKey = "AutoStash.LockedSlots.v1";

        private const string OverlayName = "AutoStashLock";

        private static readonly Color BorderColor = new Color(0.35f, 0.7f, 1f, 0.95f);

        private static readonly Color TintColor = new Color(0.3f, 0.6f, 1f, 0.14f);

        private static readonly HashSet<int> Empty = new HashSet<int>();

        private static Player _player;

        private static string _raw;

        private static HashSet<int> _locked = new HashSet<int>();

        private static int Key(Vector2i pos)
        {
            return pos.y * 1000 + pos.x;
        }

        private static HashSet<int> Current()
        {
            Player player = Player.m_localPlayer;
            if (!player)
            {
                return Empty;
            }
            player.m_customData.TryGetValue(DataKey, out string raw);
            raw = raw ?? "";
            if (player != _player || raw != _raw)
            {
                _player = player;
                _raw = raw;
                _locked = Parse(raw);
            }
            return _locked;
        }

        private static HashSet<int> Parse(string raw)
        {
            HashSet<int> result = new HashSet<int>();
            foreach (string part in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] xy = part.Split(',');
                if (xy.Length == 2
                    && int.TryParse(xy[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                    && int.TryParse(xy[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)
                    && x >= 0 && x < 1000 && y >= 0 && y < 1000)
                {
                    result.Add(Key(new Vector2i(x, y)));
                }
            }
            return result;
        }

        internal static bool IsLocked(Vector2i pos)
        {
            return Current().Contains(Key(pos));
        }

        // Only the regular inventory grid can be locked; ExtraSlots' equipment, food and ammo slots are
        // never stashed anyway.
        internal static bool Lockable(Vector2i pos)
        {
            Player player = Player.m_localPlayer;
            if (!player)
            {
                return false;
            }
            Inventory inventory = player.GetInventory();
            return pos.x >= 0 && pos.y >= 0 && pos.x < inventory.GetWidth() && pos.y < ExtraSlotsCompat.PlayerRows(inventory) && !ExtraSlotsCompat.IsSlot(pos);
        }

        internal static bool Protects(ItemDrop.ItemData item)
        {
            return item != null && IsLocked(item.m_gridPos) && Lockable(item.m_gridPos);
        }

        internal static void Toggle(Vector2i pos)
        {
            Player player = Player.m_localPlayer;
            if (!player)
            {
                return;
            }
            HashSet<int> locked = new HashSet<int>(Current());
            if (!locked.Remove(Key(pos)))
            {
                locked.Add(Key(pos));
            }
            List<string> parts = new List<string>();
            foreach (int key in locked)
            {
                parts.Add((key % 1000).ToString(CultureInfo.InvariantCulture) + "," + (key / 1000).ToString(CultureInfo.InvariantCulture));
            }
            parts.Sort(StringComparer.Ordinal);
            if (parts.Count == 0)
            {
                player.m_customData.Remove(DataKey);
            }
            else
            {
                player.m_customData[DataKey] = string.Join(";", parts);
            }
        }

        internal static bool ModifierHeld()
        {
            KeyCode key = Plugin.LockModifier.Value;
            if (key == KeyCode.None)
            {
                return false;
            }
            if (ZInput.GetKey(key, false))
            {
                return true;
            }
            // Either Alt/Ctrl/Shift key counts, so the setting does not depend on which side is pressed.
            KeyCode twin = key == KeyCode.LeftAlt ? KeyCode.RightAlt
                : key == KeyCode.LeftControl ? KeyCode.RightControl
                : key == KeyCode.LeftShift ? KeyCode.RightShift
                : KeyCode.None;
            return twin != KeyCode.None && ZInput.GetKey(twin, false);
        }

        internal static void UpdateOverlays(InventoryGrid grid)
        {
            bool enabled = Plugin.Enabled.Value;
            foreach (InventoryElement element in GameAccess.Elements(grid))
            {
                if (!element)
                {
                    continue;
                }
                bool show = enabled && IsLocked(element.Position) && Lockable(element.Position);
                Transform overlay = element.transform.Find(OverlayName);
                if (!overlay)
                {
                    if (!show)
                    {
                        continue;
                    }
                    overlay = CreateOverlay(element.transform);
                }
                if (overlay.gameObject.activeSelf != show)
                {
                    overlay.gameObject.SetActive(show);
                }
            }
        }

        private static Transform CreateOverlay(Transform element)
        {
            GameObject root = new GameObject(OverlayName, typeof(RectTransform));
            root.transform.SetParent(element, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(1f, 1f);
            rect.offsetMax = new Vector2(-1f, -1f);
            AddPart(rect, "Tint", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TintColor);
            const float t = 2f;
            AddPart(rect, "Top", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -t), Vector2.zero, BorderColor);
            AddPart(rect, "Bottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, t), BorderColor);
            AddPart(rect, "Left", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(t, 0f), BorderColor);
            AddPart(rect, "Right", new Vector2(1f, 0f), Vector2.one, new Vector2(-t, 0f), Vector2.zero, BorderColor);
            return root.transform;
        }

        private static void AddPart(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            GameObject part = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            part.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)part.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Image image = part.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }
    }

    // Holding the modifier while clicking a slot of the player's inventory toggles its lock instead of
    // picking the item up.
    [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
    internal static class LockClickPatch
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(InventoryGui __instance, InventoryGrid grid, Vector2i pos)
        {
            if (!Plugin.Enabled.Value || grid != __instance.m_playerGrid || GameAccess.DragObject(__instance) || !SlotLocks.ModifierHeld() || !SlotLocks.Lockable(pos))
            {
                return true;
            }
            SlotLocks.Toggle(pos);
            __instance.m_moveItemEffects.Create(__instance.transform.position, Quaternion.identity);
            return false;
        }
    }

    [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
    internal static class LockOverlayPatch
    {
        private static void Postfix(InventoryGrid __instance)
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui && __instance == gui.m_playerGrid)
            {
                SlotLocks.UpdateOverlays(__instance);
            }
        }
    }

    // The game's "Place stacks" (button or holding Use on a chest) would also empty locked slots, so
    // their items are hidden from it for the duration of the call.
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll))]
    internal static class PlaceStacksPatch
    {
        // StackAll never calls itself, so one pending set is enough.
        private static Inventory _inventory;

        private static readonly List<KeyValuePair<int, ItemDrop.ItemData>> Hidden = new List<KeyValuePair<int, ItemDrop.ItemData>>();

        private static void Prefix(Inventory fromInventory)
        {
            Restore();
            Player player = Player.m_localPlayer;
            if (!Plugin.Enabled.Value || !Plugin.ProtectFromPlaceStacks.Value || !player || fromInventory != player.GetInventory())
            {
                return;
            }
            List<ItemDrop.ItemData> items = GameAccess.Items(fromInventory);
            for (int i = items.Count - 1; i >= 0; i--)
            {
                ItemDrop.ItemData item = items[i];
                if (SlotLocks.Protects(item) && !player.IsItemEquiped(item))
                {
                    Hidden.Add(new KeyValuePair<int, ItemDrop.ItemData>(i, item));
                    items.RemoveAt(i);
                }
            }
            if (Hidden.Count > 0)
            {
                _inventory = fromInventory;
            }
        }

        private static Exception Finalizer(Exception __exception)
        {
            Restore();
            return __exception;
        }

        private static void Restore()
        {
            Inventory inventory = _inventory;
            if (inventory == null)
            {
                Hidden.Clear();
                return;
            }
            _inventory = null;
            List<ItemDrop.ItemData> items = GameAccess.Items(inventory);
            // Removed from the highest index down, so restoring in reverse order puts every item back in place.
            for (int i = Hidden.Count - 1; i >= 0; i--)
            {
                KeyValuePair<int, ItemDrop.ItemData> removed = Hidden[i];
                if (!items.Contains(removed.Value))
                {
                    items.Insert(Math.Min(removed.Key, items.Count), removed.Value);
                }
            }
            Hidden.Clear();
            try
            {
                // The game recalculated the weight without the hidden items.
                GameAccess.Changed(inventory);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("Could not refresh the inventory after Place stacks: " + ex.Message);
            }
        }
    }

    // Soft integration with shudnal's ExtraSlots: its equipment, quick, food and ammo slots live in extra
    // inventory rows and must never be stashed or locked.
    internal static class ExtraSlotsCompat
    {
        internal const string Guid = "shudnal.ExtraSlots";

        private static bool _initialized;

        private static Func<ItemDrop.ItemData, bool> _isItemInSlot;

        private static Func<Vector2i, bool> _isGridPositionASlot;

        private static Func<int> _playerRows;

        private static void Init()
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out PluginInfo info) || !info.Instance)
            {
                return;
            }
            try
            {
                Type api = info.Instance.GetType().Assembly.GetType("ExtraSlots.API");
                if (api == null)
                {
                    Plugin.Log?.LogWarning("ExtraSlots API not found; its slots are not recognised.");
                    return;
                }
                MethodInfo itemInSlot = AccessTools.Method(api, "IsItemInSlot", new[] { typeof(ItemDrop.ItemData) });
                MethodInfo positionIsSlot = AccessTools.Method(api, "IsGridPositionASlot", new[] { typeof(Vector2i) });
                MethodInfo playerRows = AccessTools.Method(api, "GetInventoryHeightPlayer", Type.EmptyTypes);
                _isItemInSlot = itemInSlot != null ? (Func<ItemDrop.ItemData, bool>)Delegate.CreateDelegate(typeof(Func<ItemDrop.ItemData, bool>), itemInSlot) : null;
                _isGridPositionASlot = positionIsSlot != null ? (Func<Vector2i, bool>)Delegate.CreateDelegate(typeof(Func<Vector2i, bool>), positionIsSlot) : null;
                _playerRows = playerRows != null ? (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), playerRows) : null;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("ExtraSlots integration unavailable: " + ex.Message);
            }
        }

        internal static bool IsItemInSlot(ItemDrop.ItemData item)
        {
            Init();
            try
            {
                return _isItemInSlot != null && _isItemInSlot(item);
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsSlot(Vector2i pos)
        {
            Init();
            try
            {
                return _isGridPositionASlot != null && _isGridPositionASlot(pos);
            }
            catch
            {
                return false;
            }
        }

        // Rows of the regular inventory grid; ExtraSlots appends its slots below them.
        internal static int PlayerRows(Inventory inventory)
        {
            Init();
            int height = inventory.GetHeight();
            try
            {
                int rows = _playerRows != null ? _playerRows() : height;
                return rows > 0 ? Math.Min(rows, height) : height;
            }
            catch
            {
                return height;
            }
        }
    }
}
