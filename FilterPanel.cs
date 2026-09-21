using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoStash
{
    // Editor for the filter of the chest that is currently open: category toggles plus a searchable grid
    // of items. Picked items are gold, items accepted through a category are green.
    internal sealed class FilterPanel
    {
        private const float Width = 780f;

        private const float Height = 620f;

        private const float CellSize = 56f;

        private const float CellGap = 6f;

        private const int Columns = 11;

        private static readonly Color CellIdle = new Color(0.12f, 0.09f, 0.06f, 0.95f);

        private static readonly Color CellPicked = new Color(0.86f, 0.62f, 0.18f, 1f);

        private static readonly Color CellCategory = new Color(0.28f, 0.5f, 0.3f, 1f);

        private static readonly Color CategoryOn = new Color(1f, 0.8f, 0.4f);

        private static readonly Color ErrorColor = new Color(1f, 0.5f, 0.4f);

        private sealed class Entry
        {
            internal string Prefab;

            internal ItemDrop.ItemData.SharedData Shared;

            internal Sprite Icon;

            internal string Name;

            internal string Search;

            internal ItemCategory Category;

            internal GameObject Cell;

            internal Image Background;
        }

        private readonly UiKit _kit;

        private readonly List<Entry> _entries = new List<Entry>();

        private readonly List<KeyValuePair<ItemCategory, Button>> _categoryButtons = new List<KeyValuePair<ItemCategory, Button>>();

        private GameObject _canvas;

        private RectTransform _panel;

        private ScrollRect _scroll;

        private Container _chest;

        private string _expected;

        private ChestFilter _draft;

        private TMP_InputField _search;

        private TMP_Text _showLabel;

        private TMP_Text _info;

        private TMP_Text _status;

        private bool _acceptedOnly;

        private Entry _hovered;

        internal FilterPanel(UiKit kit)
        {
            _kit = kit;
        }

        internal bool IsOpen => _canvas;

        internal void Open(Container chest)
        {
            if (IsOpen)
            {
                return;
            }
            if (!FilterStore.CanEdit(chest, out string reason))
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, reason);
                return;
            }
            _chest = chest;
            _expected = FilterStore.Raw(chest);
            _draft = FilterStore.Load(chest).Copy();
            _acceptedOnly = false;
            _hovered = null;
            UITooltip.HideTooltip();
            _kit.Capture(InventoryGui.instance);
            Build();
        }

        internal void Tick()
        {
            if (!IsOpen)
            {
                return;
            }
            InventoryGui gui = InventoryGui.instance;
            Player player = Player.m_localPlayer;
            if (!Plugin.Enabled.Value || !gui || !gui.m_container.gameObject.activeInHierarchy || GameAccess.OpenedChest() != _chest || !player || player.IsDead())
            {
                Close();
                return;
            }
            if (BackPressed())
            {
                Close();
            }
        }

        internal void Close()
        {
            if (_canvas)
            {
                UnityEngine.Object.Destroy(_canvas);
                InputBlock.MarkClosed();
            }
            _canvas = null;
            _chest = null;
            _hovered = null;
            _entries.Clear();
            _categoryButtons.Clear();
        }

        private static bool BackPressed()
        {
            InputBlock.Polling = true;
            try
            {
                return ZInput.GetKeyDown(KeyCode.Escape, false) || ZInput.GetButtonDown("JoyButtonB");
            }
            finally
            {
                InputBlock.Polling = false;
            }
        }

        private void Build()
        {
            _canvas = new GameObject("AutoStashCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas inventoryCanvas = InventoryGui.instance.GetComponentInParent<Canvas>();
            canvas.sortingOrder = inventoryCanvas ? inventoryCanvas.sortingOrder + 120 : 1020;
            CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 800f);
            scaler.matchWidthOrHeight = 1f;

            Image backdrop = UiKit.AddImage("Backdrop", _canvas.transform, new Color(0f, 0f, 0f, 0.7f));
            UiKit.Stretch(backdrop.rectTransform);

            Image panel = UiKit.AddImage("Panel", _canvas.transform, _kit.PanelColor);
            panel.sprite = _kit.PanelSprite;
            panel.type = _kit.PanelType;
            _panel = panel.rectTransform;
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = Vector2.zero;
            _panel.sizeDelta = new Vector2(Width, Height);

            _kit.AddText(_panel, Text.T("Chest filter", "Фильтр сундука"), 24f, 16f, 732f, 32f, 24f);
            TMP_Text hint = _kit.AddText(_panel, Text.T(
                $"The chest accepts the picked items and everything in the picked categories. Press {Plugin.StashKeyName()} near it to stash.",
                $"Сундук принимает выбранные предметы и всё из выбранных категорий. Нажмите {Plugin.StashKeyName()} рядом, чтобы разложить вещи."), 24f, 48f, 732f, 24f, 15f);
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 11f;
            hint.fontSizeMax = 15f;

            _kit.AddText(_panel, Text.T("Categories (include items found later):", "Категории (включая предметы, найденные позже):"), 24f, 78f, 732f, 24f, 17f);
            for (int i = 0; i < ItemCategories.All.Length; i++)
            {
                ItemCategory category = ItemCategories.All[i];
                Button button = _kit.AddButton(_panel, category.Title, 24f + i % 5 * 148f, 106f + i / 5 * 38f, 140f, 32f, () => ToggleCategory(category));
                _categoryButtons.Add(new KeyValuePair<ItemCategory, Button>(category, button));
            }

            _search = _kit.AddInput(_panel, Text.T("Search items…", "Поиск предметов…"), 24f, 188f, 330f, 34f, 40);
            _search.onValueChanged.AddListener(_ => ApplyVisibility(true));
            _showLabel = _kit.AddButton(_panel, "", 362f, 188f, 180f, 34f, () =>
            {
                _acceptedOnly = !_acceptedOnly;
                Refresh();
                ResetScroll();
            }).GetComponentInChildren<TMP_Text>();
            _kit.AddButton(_panel, Text.T("Add chest contents", "Добавить содержимое"), 550f, 188f, 206f, 34f, AddChestContents);

            BuildGrid();

            _info = _kit.AddText(_panel, "", 24f, 530f, 732f, 24f, 16f);
            _status = _kit.AddText(_panel, "", 24f, 554f, 732f, 22f, 14f);
            _status.color = ErrorColor;

            _kit.AddButton(_panel, Text.T("Clear all", "Очистить"), 24f, 580f, 150f, 34f, () =>
            {
                _draft.Items.Clear();
                _draft.Categories.Clear();
                _status.text = "";
                Refresh();
            });
            _kit.AddButton(_panel, Text.T("Cancel", "Отмена"), 536f, 580f, 105f, 34f, Close);
            _kit.AddButton(_panel, Text.T("Save", "Сохранить"), 651f, 580f, 105f, 34f, Save);

            Refresh();
        }

        private void BuildGrid()
        {
            RectTransform view = UiKit.AddRect("Items", _panel, 24f, 232f, 716f, 290f);
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            view.gameObject.AddComponent<RectMask2D>();

            RectTransform content = (RectTransform)new GameObject("Content", typeof(RectTransform)).transform;
            content.SetParent(view, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(CellSize, CellSize);
            grid.spacing = new Vector2(CellGap, CellGap);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Columns;
            grid.childAlignment = TextAnchor.UpperCenter;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll = view.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.inertia = false;
            _scroll.scrollSensitivity = 40f;
            _scroll.viewport = view;
            _scroll.content = content;

            RectTransform bar = UiKit.AddRect("Scrollbar", _panel, 744f, 232f, 12f, 290f);
            bar.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            RectTransform area = UiKit.AddRect("Area", bar, 0f, 0f, 0f, 0f);
            UiKit.Stretch(area);
            Image handle = UiKit.AddImage("Handle", area, new Color(0.85f, 0.65f, 0.35f, 0.9f));
            UiKit.Stretch(handle.rectTransform);
            Scrollbar scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            _scroll.verticalScrollbar = scrollbar;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            foreach (Entry entry in Catalog())
            {
                AddCell(content, entry);
                _entries.Add(entry);
            }
        }

        private void AddCell(Transform content, Entry entry)
        {
            GameObject cell = new GameObject("Item", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(HoverRelay));
            cell.transform.SetParent(content, false);
            Image background = cell.GetComponent<Image>();
            background.color = CellIdle;
            Button button = cell.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => ToggleItem(entry));
            HoverRelay hover = cell.GetComponent<HoverRelay>();
            hover.Entered = () => SetHovered(entry);
            hover.Exited = () =>
            {
                if (_hovered == entry)
                {
                    SetHovered(null);
                }
            };
            if (entry.Icon)
            {
                Image icon = UiKit.AddImage("Icon", cell.transform, Color.white);
                UiKit.Stretch(icon.rectTransform, 5f);
                icon.sprite = entry.Icon;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }
            else
            {
                TMP_Text name = _kit.AddText(cell.transform, entry.Name, 0f, 0f, 0f, 0f, 10f);
                UiKit.Stretch(name.rectTransform, 3f);
                name.alignment = TextAlignmentOptions.Center;
                name.textWrappingMode = TextWrappingModes.Normal;
            }
            entry.Cell = cell;
            entry.Background = background;
        }

        // Items this character knows (or everything, if configured), plus whatever is in the chest, the
        // inventory or already in the filter.
        private List<Entry> Catalog()
        {
            Player player = Player.m_localPlayer;
            HashSet<string> present = new HashSet<string>(StringComparer.Ordinal);
            AddPresent(present, _chest.GetInventory());
            if (player)
            {
                AddPresent(present, player.GetInventory());
            }
            Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
            ObjectDB db = ObjectDB.instance;
            if (db)
            {
                foreach (GameObject prefab in db.m_items)
                {
                    ItemDrop drop = prefab ? prefab.GetComponent<ItemDrop>() : null;
                    ItemDrop.ItemData.SharedData shared = drop && drop.m_itemData != null ? drop.m_itemData.m_shared : null;
                    if (shared == null || string.IsNullOrEmpty(shared.m_name) || shared.m_icons == null || shared.m_icons.Length == 0 || !shared.m_icons[0]
                        || !ChestFilter.ValidName(prefab.name) || entries.ContainsKey(prefab.name))
                    {
                        continue;
                    }
                    bool listed = Plugin.ShowUndiscovered.Value || _draft.Items.Contains(prefab.name) || present.Contains(prefab.name) || (player && player.IsMaterialKnown(shared.m_name));
                    if (!listed)
                    {
                        continue;
                    }
                    string name = Text.Localize(shared.m_name);
                    entries[prefab.name] = new Entry
                    {
                        Prefab = prefab.name,
                        Shared = shared,
                        Icon = shared.m_icons[0],
                        Name = name,
                        Search = (name + " " + prefab.name).ToLowerInvariant(),
                        Category = ItemCategories.Of(shared)
                    };
                }
            }
            // Picked items that no longer exist (for example from a removed mod) stay visible so they can be removed.
            foreach (string prefab in _draft.Items)
            {
                if (!entries.ContainsKey(prefab))
                {
                    entries[prefab] = new Entry { Prefab = prefab, Name = prefab, Search = prefab.ToLowerInvariant() };
                }
            }
            return entries.Values
                .OrderBy(e => e.Category != null ? ItemCategories.IndexOf(e.Category) : int.MaxValue)
                .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static void AddPresent(HashSet<string> present, Inventory inventory)
        {
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                string prefab = ChestFilter.PrefabName(item);
                if (prefab != null)
                {
                    present.Add(prefab);
                }
            }
        }

        private int Rank(Entry entry)
        {
            return _draft.Rank(entry.Prefab, entry.Shared);
        }

        private void ToggleItem(Entry entry)
        {
            if (!_draft.Items.Remove(entry.Prefab))
            {
                _draft.Items.Add(entry.Prefab);
            }
            _status.text = "";
            if (_acceptedOnly)
            {
                ApplyVisibility(false);
            }
            else
            {
                RefreshCell(entry);
            }
            UpdateInfo();
        }

        private void ToggleCategory(ItemCategory category)
        {
            if (!_draft.Categories.Remove(category.Id))
            {
                _draft.Categories.Add(category.Id);
            }
            _status.text = "";
            Refresh();
        }

        private void AddChestContents()
        {
            int added = 0;
            foreach (ItemDrop.ItemData item in _chest.GetInventory().GetAllItems())
            {
                string prefab = ChestFilter.PrefabName(item);
                if (prefab != null && ChestFilter.ValidName(prefab) && _draft.Items.Add(prefab))
                {
                    added++;
                }
            }
            _status.text = _chest.GetInventory().NrOfItems() == 0
                ? Text.T("The chest is empty.", "Сундук пуст.")
                : added == 0 ? Text.T("Everything in the chest is already picked.", "Всё содержимое сундука уже выбрано.") : "";
            Refresh();
        }

        private void SetHovered(Entry entry)
        {
            Entry previous = _hovered;
            _hovered = entry;
            if (previous != null)
            {
                RefreshCell(previous);
            }
            if (entry != null)
            {
                RefreshCell(entry);
            }
            UpdateInfo();
        }

        private void Refresh()
        {
            if (!_panel)
            {
                return;
            }
            foreach (KeyValuePair<ItemCategory, Button> pair in _categoryButtons)
            {
                bool on = _draft.Categories.Contains(pair.Key.Id);
                pair.Value.image.color = on ? CategoryOn : _kit.ButtonColor;
                TMP_Text label = pair.Value.GetComponentInChildren<TMP_Text>();
                label.color = on ? Color.white : UiKit.TextColor;
                label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            }
            _showLabel.text = _acceptedOnly ? Text.T("Showing: accepted", "Показаны: принимаемые") : Text.T("Showing: all", "Показаны: все");
            ApplyVisibility(false);
            UpdateInfo();
        }

        private void ApplyVisibility(bool resetScroll)
        {
            string query = (_search ? _search.text : "").Trim().ToLowerInvariant();
            foreach (Entry entry in _entries)
            {
                bool visible = (query.Length == 0 || entry.Search.Contains(query)) && (!_acceptedOnly || Rank(entry) != ChestFilter.RankNone);
                if (entry.Cell.activeSelf != visible)
                {
                    entry.Cell.SetActive(visible);
                }
                if (!visible && _hovered == entry)
                {
                    _hovered = null;
                }
                RefreshCell(entry);
            }
            if (resetScroll)
            {
                ResetScroll();
            }
            UpdateInfo();
        }

        private void ResetScroll()
        {
            if (_scroll)
            {
                _scroll.verticalNormalizedPosition = 1f;
            }
        }

        private void RefreshCell(Entry entry)
        {
            int rank = Rank(entry);
            Color color = rank == ChestFilter.RankPicked ? CellPicked : rank == ChestFilter.RankCategory ? CellCategory : CellIdle;
            entry.Background.color = entry == _hovered ? Color.Lerp(color, Color.white, 0.3f) : color;
        }

        private void UpdateInfo()
        {
            if (!_info)
            {
                return;
            }
            if (_hovered != null)
            {
                int rank = Rank(_hovered);
                string category = _hovered.Category != null ? _hovered.Category.Title : "";
                string state = rank == ChestFilter.RankPicked
                    ? Text.T("accepted, picked (click to remove)", "принимается, выбран (нажмите, чтобы убрать)")
                    : rank == ChestFilter.RankCategory
                        ? Text.T($"accepted via \"{category}\" (click to pick it too)", $"принимается через «{category}» (нажмите, чтобы выбрать отдельно)")
                        : Text.T("not accepted (click to add)", "не принимается (нажмите, чтобы добавить)");
                _info.text = _hovered.Name + ": " + state;
                return;
            }
            int items = _draft.Items.Count;
            int categories = _draft.Categories.Count;
            _info.text = _draft.IsEmpty
                ? Text.T("The chest accepts nothing yet. Pick categories or click items.", "Пока сундук ничего не принимает. Выберите категории или нажмите на предметы.")
                : Text.T($"Picked items: {items}. Categories: {categories}.", $"Выбрано предметов: {items}. Категорий: {categories}.");
        }

        private void Save()
        {
            if (!IsOpen)
            {
                return;
            }
            if (FilterStore.Save(_chest, _draft, _expected, out string message))
            {
                Close();
            }
            else
            {
                _status.text = message;
            }
        }
    }
}
