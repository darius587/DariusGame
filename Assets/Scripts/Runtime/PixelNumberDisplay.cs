using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class PixelNumberDisplay : MonoBehaviour
    {
        [SerializeField] private Sprite[] glyphs = Array.Empty<Sprite>(); // 0..9, then slash.
        [SerializeField] private Image[] slots = Array.Empty<Image>();
        [SerializeField] private Color color = Color.white;
        private string displayedValue;
        public string DisplayedValue => displayedValue;

        public void SetValue(string value, bool force = false)
        {
            if (!force && displayedValue == value) return;
            if (glyphs.Length != 11) return;
            if (slots.Length < value.Length)
            {
                int previous = slots.Length;
                Array.Resize(ref slots, value.Length);
                for (int i = previous; i < slots.Length; i++)
                {
                    var go = new GameObject("Glyph " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.transform.SetParent(transform, false);
                    slots[i] = go.GetComponent<Image>();
                }
            }
            for (int i = 0; i < slots.Length; i++)
            {
                Image image = slots[i];
                if (image == null) continue;
                bool visible = i < value.Length;
                image.gameObject.SetActive(visible);
                if (!visible) continue;
                int glyph = value[i] == '/' ? 10 : value[i] - '0';
                image.sprite = glyph >= 0 && glyph < glyphs.Length ? glyphs[glyph] : null;
                image.color = color;
                image.raycastTarget = false;
                image.preserveAspect = true;
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 0.5f);
                rect.sizeDelta = new Vector2(12, 20);
                rect.anchoredPosition = new Vector2(i * 16, 0);
            }
            displayedValue = value;
        }
    }
}
