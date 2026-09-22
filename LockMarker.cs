using UnityEngine;
using UnityEngine.UI;

namespace AutoStash
{
    // Marks a locked inventory slot with a thin frame along its edges. The frame stays clear of everything the
    // game draws inside the slot (stack count, durability bar, hotbar number, quality), and its colour is kept
    // away from the blue the game uses for equipped items.
    internal sealed class LockMarker : MonoBehaviour
    {
        internal const string ObjectName = "AutoStashLock";

        private const float FrameWidth = 2f;

        private Image[] _edges;

        internal static LockMarker Create(Transform element)
        {
            GameObject root = new GameObject(ObjectName, typeof(RectTransform));
            root.transform.SetParent(element, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            LockMarker marker = root.AddComponent<LockMarker>();
            marker._edges = new[]
            {
                Edge(rect, "Top", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -FrameWidth), Vector2.zero),
                Edge(rect, "Bottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, FrameWidth)),
                Edge(rect, "Left", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(FrameWidth, 0f)),
                Edge(rect, "Right", new Vector2(1f, 0f), Vector2.one, new Vector2(-FrameWidth, 0f), Vector2.zero)
            };
            return marker;
        }

        internal void Apply(Color color)
        {
            foreach (Image edge in _edges)
            {
                edge.color = color;
            }
        }

        private static Image Edge(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject edge = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            edge.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)edge.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Image image = edge.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
