using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace AutoStash
{
    // Moves inventory items into nearby chests whose filter accepts them.
    //
    // A chest's contents may only be changed by the owner of its ZDO. Chests the local player already owns
    // are filled at once; for the others the game's own "Place stacks" handshake (RPC_RequestStack) is
    // used: the current owner checks that the chest is not in use and hands ownership over. This works
    // with vanilla servers and with players who do not have the mod. All answers are collected first, so
    // the priority order between chests does not depend on network timing.
    internal static class Stasher
    {
        private const float TimeoutSeconds = 3f;

        private static readonly HashSet<Container> Chests = new HashSet<Container>();

        private static Session _session;

        private sealed class Target
        {
            internal Container Chest;

            internal float Distance;

            internal ChestFilter Filter;
        }

        private sealed class Session
        {
            internal readonly List<Target> Ready = new List<Target>();

            internal readonly Dictionary<Container, Target> Requested = new Dictionary<Container, Target>();

            internal readonly Dictionary<Container, Target> Granted = new Dictionary<Container, Target>();

            internal float Deadline;

            internal int Unavailable;
        }

        internal static void Register(Container chest)
        {
            Chests.Add(chest);
        }

        internal static void Abort()
        {
            _session = null;
        }

        internal static void Begin()
        {
            Player player = Player.m_localPlayer;
            if (_session != null || !player)
            {
                return;
            }
            long playerId = Game.instance.GetPlayerProfile().GetPlayerID();
            List<Target> targets = FindTargets(player, playerId, out int inUse);
            if (targets.Count == 0)
            {
                player.Message(MessageHud.MessageType.Center, inUse > 0
                    ? Text.T("The chests nearby are in use.", "Сундуки рядом заняты другими игроками.")
                    : Text.T("No chests with a filter nearby.", "Рядом нет сундуков с фильтром."));
                return;
            }
            if (!Candidates(player).Any(item => targets.Any(t => t.Filter.Rank(item) != ChestFilter.RankNone)))
            {
                player.Message(MessageHud.MessageType.Center, Text.T("Nothing to stash.", "Нечего раскладывать."));
                return;
            }

            Session session = new Session { Deadline = Time.unscaledTime + TimeoutSeconds, Unavailable = inUse };
            foreach (Target target in targets)
            {
                ZNetView view = FilterStore.NetView(target.Chest);
                if (view.IsOwner())
                {
                    session.Ready.Add(target);
                }
                else if (!view.GetZDO().HasOwner())
                {
                    view.ClaimOwnership();
                    session.Ready.Add(target);
                }
                else
                {
                    session.Requested[target.Chest] = target;
                }
            }
            _session = session;
            foreach (Container chest in session.Requested.Keys.ToList())
            {
                FilterStore.NetView(chest).InvokeRPC("RPC_RequestStack", playerId);
            }
            Tick();
        }

        internal static void Tick()
        {
            Session session = _session;
            if (session == null)
            {
                return;
            }
            // The previous owner sends the chest's latest data together with the ownership change, so the
            // chest can be filled once the local player is its owner.
            foreach (KeyValuePair<Container, Target> pair in session.Granted.ToList())
            {
                ZNetView view = FilterStore.NetView(pair.Key);
                if (!pair.Key || !view || !view.IsValid())
                {
                    session.Granted.Remove(pair.Key);
                }
                else if (view.IsOwner())
                {
                    session.Granted.Remove(pair.Key);
                    session.Ready.Add(pair.Value);
                }
            }
            bool waiting = session.Requested.Count > 0 || session.Granted.Count > 0;
            if (waiting && Time.unscaledTime < session.Deadline)
            {
                return;
            }
            session.Unavailable += session.Requested.Count + session.Granted.Count;
            _session = null;
            Execute(session);
        }

        // Returns true when the answer belonged to a stash request, so the game's Place stacks is skipped.
        internal static bool OnStackResponse(Container chest, bool granted)
        {
            Session session = _session;
            if (session == null || !session.Requested.TryGetValue(chest, out Target target))
            {
                return false;
            }
            session.Requested.Remove(chest);
            if (granted)
            {
                session.Granted[chest] = target;
            }
            else
            {
                session.Unavailable++;
            }
            return true;
        }

        private static List<Target> FindTargets(Player player, long playerId, out int inUse)
        {
            inUse = 0;
            List<Target> targets = new List<Target>();
            Vector3 center = player.transform.position;
            float radius = Plugin.Radius.Value;
            Chests.RemoveWhere(c => !c);
            foreach (Container chest in Chests)
            {
                if (!chest.gameObject.activeInHierarchy)
                {
                    continue;
                }
                float distance = Vector3.Distance(center, chest.transform.position);
                if (distance > radius || !FilterStore.Eligible(chest))
                {
                    continue;
                }
                ZNetView view = FilterStore.NetView(chest);
                if (!view || !view.IsValid())
                {
                    continue;
                }
                ChestFilter filter = FilterStore.Load(chest);
                if (filter.IsEmpty || !CanAccess(chest, playerId))
                {
                    continue;
                }
                // Someone else has the chest open; the request would be refused anyway.
                if (!view.IsOwner() && view.GetZDO().GetInt(ZDOVars.s_inUse) == 1)
                {
                    inUse++;
                    continue;
                }
                targets.Add(new Target { Chest = chest, Distance = distance, Filter = filter });
            }
            return targets;
        }

        // Same rules the game applies when a chest is opened: wards, then the chest's privacy setting.
        private static bool CanAccess(Container chest, long playerId)
        {
            if (chest.m_checkGuardStone && !PrivateArea.CheckAccess(chest.transform.position, 0f, false, false))
            {
                return false;
            }
            switch (chest.m_privacy)
            {
                case Container.PrivacySetting.Public:
                    return true;
                case Container.PrivacySetting.Private:
                    Piece piece = chest.GetComponent<Piece>();
                    return piece && piece.GetCreator() == playerId;
                default:
                    return false;
            }
        }

        // Items that may leave the inventory: not equipped, not in a locked slot, not in one of ExtraSlots'
        // special slots and not currently held by the mouse or the split dialog.
        private static List<ItemDrop.ItemData> Candidates(Player player)
        {
            Inventory inventory = player.GetInventory();
            int rows = ExtraSlotsCompat.PlayerRows(inventory);
            InventoryGui gui = InventoryGui.instance;
            ItemDrop.ItemData dragged = gui ? GameAccess.DragItem(gui) : null;
            ItemDrop.ItemData splitting = gui ? GameAccess.SplitItem(gui) : null;
            List<ItemDrop.ItemData> result = new List<ItemDrop.ItemData>();
            foreach (ItemDrop.ItemData item in inventory.GetAllItemsInGridOrder())
            {
                if (item == null || item.m_equipped || player.IsItemEquiped(item) || item.m_shared.m_questItem
                    || item == dragged || item == splitting
                    || item.m_gridPos.y >= rows || ExtraSlotsCompat.IsItemInSlot(item) || SlotLocks.Protects(item))
                {
                    continue;
                }
                result.Add(item);
            }
            return result;
        }

        private static void Execute(Session session)
        {
            Player player = Player.m_localPlayer;
            if (!player || player.IsDead())
            {
                return;
            }
            Inventory inventory = player.GetInventory();
            List<Target> targets = new List<Target>();
            foreach (Target target in session.Ready)
            {
                ZNetView view = FilterStore.NetView(target.Chest);
                if (!target.Chest || !view || !view.IsValid() || !view.IsOwner())
                {
                    session.Unavailable++;
                    continue;
                }
                GameAccess.Reload(target.Chest);
                target.Filter = FilterStore.Load(target.Chest);
                targets.Add(target);
            }

            int moved = 0;
            int left = 0;
            HashSet<Container> used = new HashSet<Container>();
            foreach (ItemDrop.ItemData item in Candidates(player))
            {
                List<Target> order = targets
                    .Select(t => new { Target = t, Rank = t.Filter.Rank(item) })
                    .Where(t => t.Rank != ChestFilter.RankNone)
                    .OrderBy(t => t.Rank)
                    .ThenBy(t => t.Target.Chest.GetInventory().ContainsItemByName(item.m_shared.m_name) ? 0 : 1)
                    .ThenBy(t => t.Target.Distance)
                    .Select(t => t.Target)
                    .ToList();
                if (order.Count == 0)
                {
                    continue;
                }
                foreach (Target target in order)
                {
                    int count = Move(inventory, target.Chest.GetInventory(), item);
                    if (count > 0)
                    {
                        moved += count;
                        used.Add(target.Chest);
                    }
                    if (!inventory.ContainsItem(item))
                    {
                        break;
                    }
                }
                if (inventory.ContainsItem(item))
                {
                    left += item.m_stack;
                }
            }
            if (moved > 0)
            {
                GameAccess.Changed(inventory);
                if (InventoryGui.instance)
                {
                    InventoryGui.instance.m_moveItemEffects.Create(player.transform.position, Quaternion.identity);
                }
            }
            player.Message(MessageHud.MessageType.Center, Report(moved, used.Count, left, session.Unavailable));
        }

        // Tops up existing stacks first, then uses free slots. Returns how many units were moved.
        private static int Move(Inventory from, Inventory to, ItemDrop.ItemData item)
        {
            if (!HasRoom(to, item))
            {
                return 0;
            }
            int before = item.m_stack;
            if (to.AddItem(item))
            {
                from.RemoveItem(item);
                return before;
            }
            return before - item.m_stack;
        }

        private static bool HasRoom(Inventory inventory, ItemDrop.ItemData item)
        {
            if (inventory.HaveEmptySlot())
            {
                return true;
            }
            if (item.m_shared.m_maxStackSize <= 1)
            {
                return false;
            }
            foreach (ItemDrop.ItemData other in inventory.GetAllItems())
            {
                if (other.m_shared.m_name == item.m_shared.m_name && other.m_quality == item.m_quality && other.m_worldLevel == item.m_worldLevel && other.m_stack < other.m_shared.m_maxStackSize)
                {
                    return true;
                }
            }
            return false;
        }

        private static string Report(int moved, int chests, int left, int unavailable)
        {
            List<string> lines = new List<string>();
            if (moved > 0)
            {
                lines.Add(Text.T(
                    $"Stashed {moved} item(s) in {chests} chest(s).",
                    $"Разложено {moved} {Text.Plural(moved, "предмет", "предмета", "предметов")} по {chests} {Text.Plural(chests, "сундуку", "сундукам", "сундукам")}."));
            }
            if (left > 0)
            {
                lines.Add(Text.T($"No room left for {left} item(s).", $"Не хватило места ещё для {left} шт."));
            }
            if (unavailable > 0)
            {
                lines.Add(Text.T("Some chests are in use by other players.", "Часть сундуков занята другими игроками."));
            }
            if (lines.Count == 0)
            {
                lines.Add(Text.T("Nothing to stash.", "Нечего раскладывать."));
            }
            return string.Join("\n", lines);
        }
    }

    [HarmonyPatch(typeof(Container), "Awake")]
    internal static class ContainerRegisterPatch
    {
        private static void Postfix(Container __instance)
        {
            if (__instance.GetInventory() != null)
            {
                Stasher.Register(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(Container), "RPC_StackResponse")]
    internal static class StackResponsePatch
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Container __instance, bool granted)
        {
            return !Stasher.OnStackResponse(__instance, granted);
        }
    }
}
