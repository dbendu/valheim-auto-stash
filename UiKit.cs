using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoStash
{
    // Builds uGUI widgets that borrow the look of the game's inventory window.
    internal sealed class UiKit
    {
        internal static readonly Color TextColor = new Color(1f, 0.85f, 0.57f);

        internal TMP_FontAsset Font;

        internal Sprite ButtonSprite;

        internal Image.Type ButtonType = Image.Type.Sliced;

        internal Color ButtonColor = new Color(0.29f, 0.2f, 0.13f);

        internal Sprite PanelSprite;

        internal Image.Type PanelType = Image.Type.Sliced;

        internal Color PanelColor = new Color(0.16f, 0.11f, 0.08f, 0.97f);

        internal GameObject TooltipPrefab;

        internal void Capture(InventoryGui gui)
        {
            if (!gui)
            {
                return;
            }
            if (gui.m_containerName && gui.m_containerName.font)
            {
                Font = gui.m_containerName.font;
            }
            Image button = gui.m_takeAllButton ? gui.m_takeAllButton.image : null;
            if (button && button.sprite)
            {
                ButtonSprite = button.sprite;
                ButtonType = button.type;
                ButtonColor = button.color;
            }
            Image panel = gui.m_container ? gui.m_container.GetComponent<Image>() : null;
            if (panel && panel.sprite)
            {
                PanelSprite = panel.sprite;
                PanelType = panel.type;
                PanelColor = panel.color;
            }
            if (!TooltipPrefab && gui.m_playerGrid && gui.m_playerGrid.m_elementPrefab)
            {
                InventoryElement element = gui.m_playerGrid.m_elementPrefab.GetComponent<InventoryElement>();
                if (element && element.m_tooltip && element.m_tooltip.m_tooltipPrefab)
                {
                    TooltipPrefab = element.m_tooltip.m_tooltipPrefab;
                }
            }
            if (!TooltipPrefab)
            {
                foreach (UITooltip tooltip in gui.GetComponentsInChildren<UITooltip>(true))
                {
                    if (tooltip.m_tooltipPrefab)
                    {
                        TooltipPrefab = tooltip.m_tooltipPrefab;
                        break;
                    }
                }
            }
        }

        internal static RectTransform AddRect(string name, Transform parent, float x, float y, float width, float height)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        internal static Image AddImage(string name, Transform parent, Color color)
        {
            Image image = AddRect(name, parent, 0f, 0f, 0f, 0f).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        internal static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        internal TMP_Text AddText(Transform parent, string value, float x, float y, float width, float height, float size)
        {
            TextMeshProUGUI text = AddRect("Text", parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            if (Font)
            {
                text.font = Font;
            }
            text.text = value;
            text.fontSize = size;
            text.color = TextColor;
            text.richText = false;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        internal Button AddButton(Transform parent, string label, float x, float y, float width, float height, UnityAction action)
        {
            RectTransform rect = AddRect("Button", parent, x, y, width, height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = ButtonSprite;
            image.type = ButtonType;
            image.color = ButtonColor;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            TMP_Text text = AddText(rect, label, 6f, 0f, width - 12f, height, 18f);
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 11f;
            text.fontSizeMax = 18f;
            return button;
        }

        internal TMP_InputField AddInput(Transform parent, string placeholder, float x, float y, float width, float height, int limit)
        {
            RectTransform rect = AddRect("Input", parent, x, y, width, height);
            Image background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.06f, 0.04f);
            TMP_InputField input = rect.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.characterLimit = limit;
            input.lineType = TMP_InputField.LineType.SingleLine;
            RectTransform viewport = AddRect("Viewport", rect, 8f, 0f, width - 16f, height);
            viewport.gameObject.AddComponent<RectMask2D>();
            TMP_Text text = AddText(viewport, "", 0f, 0f, width - 16f, height, 18f);
            text.overflowMode = TextOverflowModes.Overflow;
            TMP_Text hint = AddText(viewport, placeholder, 0f, 0f, width - 16f, height, 18f);
            hint.color = Color.gray;
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = hint;
            return input;
        }

        internal void AddTooltip(GameObject target, string topic, string text)
        {
            if (!TooltipPrefab)
            {
                return;
            }
            UITooltip tooltip = target.GetComponent<UITooltip>();
            if (!tooltip)
            {
                tooltip = target.AddComponent<UITooltip>();
            }
            tooltip.m_tooltipPrefab = TooltipPrefab;
            tooltip.m_topic = topic;
            tooltip.m_text = text;
        }
    }

    // Reports pointer hover to the filter window, which shows the hovered item's name and state.
    internal sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        internal Action Entered;

        internal Action Exited;

        public void OnPointerEnter(PointerEventData eventData)
        {
            Entered?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Exited?.Invoke();
        }
    }
}
