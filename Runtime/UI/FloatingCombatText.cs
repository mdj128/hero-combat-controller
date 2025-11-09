using UnityEngine;
using Random = UnityEngine.Random;

namespace HeroCharacter
{
    /// <summary>
    /// Lightweight world-space text that floats upward, fades out, and optionally billboards toward the camera.
    /// </summary>
    public class FloatingCombatText : MonoBehaviour
    {
        [SerializeField] TextMesh textMesh;
        [SerializeField] AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField, Min(0.01f)] float characterSize = 0.1f;
        [SerializeField] float baseScale = 1f;
        [SerializeField] Font fontOverride;

        Camera targetCamera;
        bool faceCamera;
        float lifetime;
        float riseSpeed;
        Vector3 driftVelocity;
        float elapsed;
        Color baseColor = Color.white;
        float scaleMultiplier = 1f;

        public static FloatingCombatText CreateRuntimeInstance(Font fallbackFont)
        {
            var go = new GameObject("FloatingCombatText");
            var instance = go.AddComponent<FloatingCombatText>();
            instance.fontOverride = fallbackFont;
            instance.EnsureTextMesh();
            return instance;
        }

        public static Font FindDefaultFont(Font current)
        {
            if (current != null)
            {
                return current;
            }

            Font font = null;
            try
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch
            {
                // ignored, will try OS font
            }

            if (font == null)
            {
                font = Font.CreateDynamicFontFromOSFont("Arial", 42);
            }

            return font;
        }

        public void Play(
            string message,
            Vector3 worldPosition,
            Color color,
            float newLifetime,
            float newRiseSpeed,
            Vector2 horizontalDrift,
            Vector2 verticalDrift,
            Camera camera,
            bool shouldFaceCamera,
            float sizeMultiplier)
        {
            EnsureTextMesh();

            transform.position = worldPosition;
            baseColor = color;
            lifetime = Mathf.Max(0.1f, newLifetime);
            riseSpeed = newRiseSpeed;
            targetCamera = camera;
            faceCamera = shouldFaceCamera;
            scaleMultiplier = Mathf.Max(0.1f, sizeMultiplier);
            elapsed = 0f;
            driftVelocity = new Vector3(
                Random.Range(horizontalDrift.x, horizontalDrift.y),
                Random.Range(verticalDrift.x, verticalDrift.y),
                Random.Range(horizontalDrift.x, horizontalDrift.y));

            textMesh.text = message;
            textMesh.color = baseColor;
            transform.localScale = Vector3.one * baseScale * scaleMultiplier;

            enabled = true;
        }

        void Awake()
        {
            EnsureTextMesh();
            enabled = false;
        }

        void Update()
        {
            if (!enabled)
            {
                return;
            }

            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / lifetime);

            Vector3 motion = Vector3.up * riseSpeed + driftVelocity;
            transform.position += motion * Time.deltaTime;

            if (faceCamera)
            {
                var cam = targetCamera != null ? targetCamera : Camera.main;
                if (cam != null)
                {
                    transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
                }
            }

            Color c = baseColor;
            c.a *= alphaCurve.Evaluate(normalized);
            textMesh.color = c;

            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        void EnsureTextMesh()
        {
            if (textMesh == null)
            {
                textMesh = GetComponent<TextMesh>();
            }

            if (textMesh == null)
            {
                textMesh = gameObject.AddComponent<TextMesh>();
            }

            var font = FindDefaultFont(fontOverride);
            if (font != null)
            {
                textMesh.font = font;
                var renderer = GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.material = font.material;
                }
            }

            textMesh.characterSize = characterSize;
            textMesh.fontSize = 48;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.richText = true;
        }
    }
}
