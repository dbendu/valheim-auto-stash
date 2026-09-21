using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoStash
{
    // The "Filter" button under the chest window, the "Stash" button beside the inventory and the filter editor.
    internal sealed class StashUi : IDisposable
    {
        private readonly UiKit _kit = new UiKit();

        private readonly FilterPanel _panel;

        private Button _filterButton;

        private TMP_Text _filterLabel;

        private Button _stashButton;

        private TMP_Text _stashLabel;

        private string _stashTextKey;

        internal StashUi()
        {
            _panel = new FilterPanel(_kit);
        }

        internal bool PanelOpen => _panel.IsOpen;

        internal void Tick()
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui && (!_filterButton || !_stashButton))
            {
                _kit.Capture(gui);
                if (!_filterButton && gui.m_container)
                {
                    CreateFilterButton(gui);
                }
                if (!_stashButton && gui.m_player)
                {
                    CreateStashButton(gui);
                }
            }
            bool enabled = Plugin.Enabled.Value;
            if (_filterButton)
            {
                Container opened = GameAccess.OpenedChest();
                bool show = enabled && FilterStore.Eligible(opened);
                if (_filterButton.gameObject.activeSelf != show)
                {
                    _filterButton.gameObject.SetActive(show);
                }
                if (show)
                {
                    int count = FilterStore.Load(opened).Count;
                    string label = Text.T("Filter", "Фильтр") + (count > 0 ? " (" + count + ")" : "");
                    if (_filterLabel.text != label)
                    {
                        _filterLabel.text = label;
                    }
                }
            }
            if (_stashButton)
            {
                bool show = enabled && Plugin.InventoryButton.Value;
                if (_stashButton.gameObject.activeSelf != show)
                {
                    _stashButton.gameObject.SetActive(show);
                }
                if (show)
                {
                    UpdateStashTexts();
                }
            }
            _panel.Tick();
        }

        internal void ClosePanel()
        {
            _panel.Close();
        }

        public void Dispose()
        {
            _panel.Close();
            if (_filterButton)
            {
                UnityEngine.Object.Destroy(_filterButton.gameObject);
            }
            if (_stashButton)
            {
                UnityEngine.Object.Destroy(_stashButton.gameObject);
            }
        }

        // Hangs below the chest window's bottom-right corner, a spot the game leaves empty.
        private void CreateFilterButton(InventoryGui gui)
        {
            _filterButton = _kit.AddButton(gui.m_container, "", 0f, 0f, 140f, 36f, () => _panel.Open(GameAccess.OpenedChest()));
            _filterButton.name = "AutoStashFilterButton";
            RectTransform rect = (RectTransform)_filterButton.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-8f, -6f);
            _filterLabel = _filterButton.GetComponentInChildren<TMP_Text>();
        }

        // The column to the right of the inventory grid holds the armor and weight badges with a free gap
        // between them; the button fills that gap.
        private void CreateStashButton(InventoryGui gui)
        {
            _stashButton = _kit.AddButton(gui.m_player, "", 0f, 0f, 100f, 36f, Stasher.Begin);
            _stashButton.name = "AutoStashButton";
            RectTransform rect = (RectTransform)_stashButton.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(-8f, -6f);
            _stashLabel = _stashButton.GetComponentInChildren<TMP_Text>();
            _stashTextKey = null;
        }

        private void UpdateStashTexts()
        {
            string key = Plugin.StashKeyName();
            string modifier = Plugin.KeyName(Plugin.LockModifier.Value);
            float radius = Plugin.Radius.Value;
            string textKey = Text.Russian + "|" + key + "|" + modifier + "|" + radius + "|" + (bool)_kit.TooltipPrefab;
            if (textKey == _stashTextKey)
            {
                return;
            }
            _stashTextKey = textKey;
            _stashLabel.text = Text.T("Stash", "Разложить") + " (" + key + ")";
            _kit.AddTooltip(_stashButton.gameObject,
                Text.T("Stash to chests", "Разложить по сундукам"),
                Text.T(
                    $"Moves items into chests within {radius:0} m whose filter accepts them. Key: {key}.\n{modifier}+click a slot to lock it: locked slots are never stashed.",
                    $"Переносит вещи в сундуки в радиусе {radius:0} м, если их фильтр принимает эти вещи. Клавиша: {key}.\n{modifier}+клик по слоту блокирует его: вещи из заблокированных слотов не раскладываются."));
        }
    }
}
