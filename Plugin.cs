using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoStash
{
    // Client-only: everything happens in the local player's UI and inventory, so the dedicated server does not load it.
    [BepInPlugin(Guid, Name, Version)]
    [BepInProcess("valheim.exe")]
    [BepInDependency(ExtraSlotsCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "dbendu.AutoStash";
        public const string Name = "Auto Stash";
        public const string Version = "1.1.0";

        private static readonly KeyCode[] ExclusiveModifiers =
        {
            KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftCommand, KeyCode.RightCommand
        };

        private readonly Harmony _harmony = new Harmony(Guid);

        internal static ManualLogSource Log { get; private set; }

        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> StashKey { get; private set; }

        internal static ConfigEntry<float> Radius { get; private set; }

        internal static ConfigEntry<KeyCode> LockModifier { get; private set; }

        internal static ConfigEntry<bool> ProtectFromPlaceStacks { get; private set; }

        internal static ConfigEntry<Color> FrameColor { get; private set; }

        internal static ConfigEntry<bool> InventoryButton { get; private set; }

        internal static ConfigEntry<bool> ShowUndiscovered { get; private set; }

        internal static StashUi Ui { get; private set; }

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true, "Enable chest filters, stashing and locked slots.");
            StashKey = Config.Bind("General", "StashKey", new KeyboardShortcut(KeyCode.Z), "Key that puts inventory items into nearby chests whose filter accepts them. Alt/Ctrl must not be held unless they are part of the shortcut.");
            Radius = Config.Bind("General", "Radius", 10f, new ConfigDescription("Chests farther than this many metres from the player are ignored.", new AcceptableValueRange<float>(2f, 50f)));
            LockModifier = Config.Bind("Locked slots", "ToggleModifier", KeyCode.LeftAlt, "Hold this key and left-click an inventory slot to lock or unlock it. Items in locked slots are never stashed.");
            ProtectFromPlaceStacks = Config.Bind("Locked slots", "ProtectFromPlaceStacks", true, "Also keep locked slots out of the game's own \"Place stacks\" action.");
            FrameColor = Config.Bind("Locked slots", "FrameColor", new Color(1f, 0.62f, 0.2f, 1f), "Colour of the frame drawn around locked slots.");
            InventoryButton = Config.Bind("Interface", "InventoryButton", true, "Show a Stash button next to the inventory.");
            ShowUndiscovered = Config.Bind("Interface", "ShowUndiscoveredItems", false, "List every item in the filter editor, not only items this character has discovered.");
            Ui = new StashUi();
            _harmony.PatchAll(typeof(Plugin).Assembly);
        }

        private void Update()
        {
            try
            {
                Ui?.Tick();
            }
            catch (Exception ex)
            {
                Logger.LogError("Filter window closed after an error: " + ex);
                Ui?.ClosePanel();
            }
            try
            {
                Stasher.Tick();
                if (Enabled.Value && StashKeyPressed())
                {
                    Stasher.Begin();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Stashing stopped after an error: " + ex);
                Stasher.Abort();
            }
        }

        private void OnDestroy()
        {
            _harmony.UnpatchSelf();
            Stasher.Abort();
            Ui?.Dispose();
            Ui = null;
        }

        private static bool StashKeyPressed()
        {
            KeyboardShortcut shortcut = StashKey.Value;
            if (shortcut.MainKey == KeyCode.None || !ZInput.GetKeyDown(shortcut.MainKey, false))
            {
                return false;
            }
            List<KeyCode> modifiers = new List<KeyCode>(shortcut.Modifiers);
            foreach (KeyCode modifier in modifiers)
            {
                if (!ZInput.GetKey(modifier, false))
                {
                    return false;
                }
            }
            // Alt+Z and similar chords belong to other mods (ExtraSlots quick slots), so a plain key must not fire with them.
            foreach (KeyCode modifier in ExclusiveModifiers)
            {
                if (!modifiers.Contains(modifier) && ZInput.GetKey(modifier, false))
                {
                    return false;
                }
            }
            return CanStashNow();
        }

        // Stashing works in the world and with the inventory open, but never while the player is typing or in a menu.
        private static bool CanStashNow()
        {
            Player player = Player.m_localPlayer;
            if (!player || player.IsDead() || player.IsTeleporting() || player.InCutscene())
            {
                return false;
            }
            if ((Chat.instance && Chat.instance.HasFocus()) || global::Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible()
                || StoreGui.IsVisible() || Minimap.IsOpen() || (TextViewer.instance && TextViewer.instance.IsVisible())
                || PlayerCustomizaton.IsBarberGuiVisible() || GameCamera.InFreeFly()
                || (Hud.instance && Hud.instance.m_buildUi && Hud.instance.m_buildUi.SearchFieldFocused))
            {
                return false;
            }
            return !TextFieldFocused() && (Ui == null || !Ui.PanelOpen);
        }

        private static bool TextFieldFocused()
        {
            GameObject selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            if (!selected)
            {
                return false;
            }
            TMP_InputField tmp = selected.GetComponent<TMP_InputField>();
            InputField legacy = selected.GetComponent<InputField>();
            return (tmp && tmp.isFocused) || (legacy && legacy.isFocused);
        }

        internal static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                    return "Alt";
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    return "Ctrl";
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                    return "Shift";
                default:
                    return key.ToString();
            }
        }

        internal static string StashKeyName()
        {
            KeyboardShortcut shortcut = StashKey.Value;
            List<string> parts = new List<string>();
            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                parts.Add(KeyName(modifier));
            }
            parts.Add(KeyName(shortcut.MainKey));
            return string.Join("+", parts);
        }
    }

    // Private game members used by several parts of the mod.
    internal static class GameAccess
    {
        internal static readonly AccessTools.FieldRef<InventoryGui, Container> CurrentContainer = AccessTools.FieldRefAccess<InventoryGui, Container>("m_currentContainer");

        internal static readonly AccessTools.FieldRef<InventoryGui, ItemDrop.ItemData> DragItem = AccessTools.FieldRefAccess<InventoryGui, ItemDrop.ItemData>("m_dragItem");

        internal static readonly AccessTools.FieldRef<InventoryGui, GameObject> DragObject = AccessTools.FieldRefAccess<InventoryGui, GameObject>("m_dragGo");

        internal static readonly AccessTools.FieldRef<InventoryGui, ItemDrop.ItemData> SplitItem = AccessTools.FieldRefAccess<InventoryGui, ItemDrop.ItemData>("m_splitItem");

        internal static readonly AccessTools.FieldRef<InventoryGrid, List<InventoryElement>> Elements = AccessTools.FieldRefAccess<InventoryGrid, List<InventoryElement>>("m_elements");

        internal static readonly AccessTools.FieldRef<Inventory, List<ItemDrop.ItemData>> Items = AccessTools.FieldRefAccess<Inventory, List<ItemDrop.ItemData>>("m_inventory");

        internal static readonly AccessTools.FieldRef<Container, ZNetView> NetView = AccessTools.FieldRefAccess<Container, ZNetView>("m_nview");

        private static readonly MethodInfo InventoryChanged = AccessTools.Method(typeof(Inventory), "Changed");

        private static readonly MethodInfo ContainerLoad = AccessTools.Method(typeof(Container), "Load");

        internal static Container OpenedChest()
        {
            InventoryGui gui = InventoryGui.instance;
            return gui ? CurrentContainer(gui) : null;
        }

        // Recalculates weight and notifies listeners after a stack was only partly moved out.
        internal static void Changed(Inventory inventory)
        {
            InventoryChanged.Invoke(inventory, new object[] { false, false });
        }

        // Picks up the newest contents from the network before the chest is modified.
        internal static void Reload(Container chest)
        {
            ContainerLoad.Invoke(chest, Array.Empty<object>());
        }
    }

    internal static class Text
    {
        internal static bool Russian => Localization.instance != null && Localization.instance.GetSelectedLanguage() == "Russian";

        internal static string T(string english, string russian)
        {
            return Russian ? russian : english;
        }

        internal static string Plural(int count, string one, string few, string many)
        {
            int n = Math.Abs(count) % 100;
            int last = n % 10;
            if (n > 10 && n < 20)
            {
                return many;
            }
            if (last == 1)
            {
                return one;
            }
            return last > 1 && last < 5 ? few : many;
        }

        internal static string Localize(string token)
        {
            return Localization.instance != null ? Localization.instance.Localize(token) : token;
        }
    }

    // While the filter window is open, keyboard input belongs to it: typing in the search field must
    // not close the inventory, open the menu or trigger hotkeys.
    internal static class InputBlock
    {
        private static int _lastActiveFrame = -10;

        internal static bool Polling;

        internal static bool Active
        {
            get
            {
                if (Polling)
                {
                    return false;
                }
                if (Plugin.Ui != null && Plugin.Ui.PanelOpen)
                {
                    _lastActiveFrame = Time.frameCount;
                    return true;
                }
                // Keep blocking for one more frame so the key that closed the window is not reused.
                return Time.frameCount <= _lastActiveFrame + 1;
            }
        }

        internal static void MarkClosed()
        {
            _lastActiveFrame = Time.frameCount;
        }
    }

    [HarmonyPatch]
    internal static class ZInputButtonPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in new[] { "GetButton", "GetButtonDown", "GetButtonUp" })
            {
                yield return AccessTools.Method(typeof(ZInput), name, new[] { typeof(string) });
            }
        }

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ref bool __result)
        {
            if (InputBlock.Active)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch]
    internal static class ZInputKeyPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in new[] { "GetKey", "GetKeyDown", "GetKeyUp" })
            {
                yield return AccessTools.Method(typeof(ZInput), name, new[] { typeof(KeyCode), typeof(bool) });
            }
        }

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(KeyCode key, ref bool __result)
        {
            if (InputBlock.Active && (key < KeyCode.Mouse0 || key > KeyCode.Mouse6))
            {
                __result = false;
            }
        }
    }
}
