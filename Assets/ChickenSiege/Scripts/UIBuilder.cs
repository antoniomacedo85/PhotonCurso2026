using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Starter.Shooter
{
    /// <summary>
    /// KIT CLASE 3 · Ayudante para crear interfaz desde código.
    /// Así UIGameFlow, UIWaves y UIPowerUps se montan solos: basta con añadir el componente
    /// a un GameObject vacío. No tiene nada de red y no hace falta leerlo en clase.
    /// </summary>
    public static class UIBuilder
    {
        public static readonly Color Cream = new Color32(0xF3, 0xE6, 0xC8, 0xFF);
        public static readonly Color Gold = new Color32(0xE8, 0xC2, 0x6A, 0xFF);
        public static readonly Color Cyan = new Color32(0x3F, 0xB8, 0xC9, 0xFF);
        public static readonly Color Orange = new Color32(0xF2, 0x8C, 0x28, 0xFF);
        public static readonly Color Red = new Color32(0xE0, 0x39, 0x3E, 0xFF);
        public static readonly Color Green = new Color32(0x7C, 0xD9, 0x6B, 0xFF);
        public static readonly Color Grey = new Color32(0xA0, 0xA8, 0xB3, 0xFF);
        public static readonly Color PanelColor = new Color(0.04f, 0.06f, 0.09f, 0.78f);

        private static TMP_FontAsset _font;

        /// <summary>Canvas a pantalla completa (1920x1080 de referencia, como el resto del juego).</summary>
        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
        {
            // Buscamos la fuente ANTES de crear textos nuevos
            FindFont();

            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var group = go.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            return canvas;
        }

        /// <summary>Rectángulo anclado a un punto de la pantalla (0,0 = abajo izquierda · 1,1 = arriba derecha).</summary>
        public static RectTransform CreateRect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size,
            float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var rect = CreateRect(parent, name, anchor, position, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();

            if (_font != null)
                text.font = _font;

            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.richText = true;
            text.raycastTarget = false;
            text.text = string.Empty;

            // Contorno oscuro para que se lea sobre el cielo y la hierba
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 220);

            return text;
        }

        public static Image CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var rect = CreateRect(parent, name, anchor, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Barra horizontal. Devuelve el relleno: se actualiza con SetBar(relleno, 0..1).</summary>
        public static RectTransform CreateBar(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size,
            Color background, Color fill)
        {
            var back = CreatePanel(parent, name, anchor, position, size, background);

            var fillImage = CreatePanel(back.transform, "Fill", Vector2.zero, Vector2.zero, Vector2.zero, fill);
            var fillRect = fillImage.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);

            return fillRect;
        }

        public static void SetBar(RectTransform fill, float ratio)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        }

        /// <summary>Usamos la misma fuente que el resto de la interfaz del juego.</summary>
        private static void FindFont()
        {
            if (_font != null)
                return;

            var texts = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var text in texts)
            {
                if (text.font != null)
                {
                    _font = text.font;
                    return;
                }
            }
        }
    }
}
