using UnityEngine;

namespace HeroCharacter
{
    /// <summary>
    /// Lightweight screen-space crosshair that renders via IMGUI so it works without extra assets.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class HeroCrosshair : MonoBehaviour
    {
        [SerializeField] bool visible = true;
        [SerializeField] Color color = new Color(1f, 1f, 1f, 0.7f);
        [SerializeField] Color interactableColor = new Color(0.4f, 1f, 0.4f, 0.9f);
        [SerializeField] float size = 14f;
        [SerializeField] float thickness = 2f;
        [SerializeField] float gap = 6f;
        [SerializeField] Vector2 viewportAnchor = new Vector2(0.5f, 0.5f);

        Texture2D pixel;
        bool hasInteractable;

        public void ApplySettings(bool enabled, Color baseColor, Color hoverColor, float lineSize, float lineThickness, float lineGap, Vector2 anchor)
        {
            visible = enabled;
            color = baseColor;
            interactableColor = hoverColor;
            size = Mathf.Max(2f, lineSize);
            thickness = Mathf.Max(1f, lineThickness);
            gap = Mathf.Max(0f, lineGap);
            viewportAnchor = new Vector2(Mathf.Clamp01(anchor.x), Mathf.Clamp01(anchor.y));
        }

        public void SetInteractableHover(bool active)
        {
            hasInteractable = active;
        }

        void OnEnable()
        {
            EnsurePixel();
        }

        void OnGUI()
        {
            if (!visible || Event.current.type != EventType.Repaint)
            {
                return;
            }

            EnsurePixel();

            var drawColor = hasInteractable ? interactableColor : color;
            GUI.color = drawColor;

            float centerX = Screen.width * viewportAnchor.x;
            float centerY = Screen.height * viewportAnchor.y;

            // Vertical line
            DrawRect(new Rect(centerX - (thickness * 0.5f), centerY - gap - size, thickness, size));
            DrawRect(new Rect(centerX - (thickness * 0.5f), centerY + gap, thickness, size));

            // Horizontal line
            DrawRect(new Rect(centerX - gap - size, centerY - (thickness * 0.5f), size, thickness));
            DrawRect(new Rect(centerX + gap, centerY - (thickness * 0.5f), size, thickness));
        }

        void DrawRect(Rect rect)
        {
            GUI.DrawTexture(rect, pixel);
        }

        void EnsurePixel()
        {
            if (pixel != null)
            {
                return;
            }

            pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "HeroCrosshair_Pixel"
            };
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
            pixel.hideFlags = HideFlags.HideAndDontSave;
        }
    }
}
