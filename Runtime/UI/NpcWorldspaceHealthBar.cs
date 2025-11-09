using UnityEngine;
using UnityEngine.UI;

namespace HeroCharacter
{
    /// <summary>
    /// Builds a world-space health bar for NPCs and listens to CharacterCombatAgent events so it always reflects real damage.
    /// </summary>
    [DisallowMultipleComponent]
    public class NpcWorldspaceHealthBar : MonoBehaviour
    {
        [Header("Agent")]
        [SerializeField] CharacterCombatAgent combatAgent;
        [SerializeField] bool autoBindAgent = true;

        [Header("Placement")]
        [SerializeField] Transform anchor;
        [SerializeField] Vector3 worldOffset = new Vector3(0f, 2f, 0f);
        [SerializeField, Min(0.01f)] float uniformScale = 1f;
        [SerializeField] Vector2 barSize = new Vector2(1.2f, 0.18f);

        [Header("Visuals")]
        [SerializeField] bool alwaysVisible;
        [SerializeField] bool faceCamera = true;
        [SerializeField] Camera cameraOverride;
        [SerializeField] int sortingOrder = 10;
        [SerializeField] Color backgroundColor = new Color(0f, 0f, 0f, 0.7f);
        [SerializeField] Color fillColor = new Color(0.92f, 0.2f, 0.2f, 1f);
        [SerializeField] Color damageColor = new Color(1f, 0.8f, 0.25f, 0.75f);

        [Header("Animation")]
        [SerializeField, Min(0.01f)] float fillLerpSpeed = 10f;
        [SerializeField, Min(0.01f)] float damageLerpSpeed = 3f;
        [SerializeField, Min(0f)] float fadeSpeed = 6f;
        [SerializeField, Min(0f)] float visibilityDuration = 2f;

        [Header("Runtime UI (optional)")]
        [SerializeField] RectTransform barRoot;
        [SerializeField] Image backgroundImage;
        [SerializeField] Image fillImage;
        [SerializeField] Image damageImage;
        [SerializeField] Slider slider;
        [SerializeField] Canvas canvas;
        [SerializeField] CanvasGroup canvasGroup;

        float targetFill = 1f;
        float currentFill = 1f;
        float trailingFill = 1f;
        float visibleTimer;
        bool isDead;
        bool eventsHooked;

        const string RuntimeRootName = "NPC Health Bar";
        static Sprite defaultSprite;
        static Texture2D defaultTexture;

        public Transform Anchor
        {
            get => anchor != null ? anchor : transform;
            set => anchor = value;
        }

        public Vector3 WorldOffset
        {
            get => worldOffset;
            set => worldOffset = value;
        }

        public Vector2 BarSize
        {
            get => barSize;
            set
            {
                barSize = value;
                ApplySize();
            }
        }

        public float UniformScale
        {
            get => uniformScale;
            set
            {
                uniformScale = Mathf.Max(0.01f, value);
                ApplyScale();
            }
        }

        public bool AlwaysVisible
        {
            get => alwaysVisible;
            set
            {
                alwaysVisible = value;
                ForceUpdateVisuals();
            }
        }

        void Reset()
        {
            TryAutoAssignAgent();
        }

        void Awake()
        {
            TryAutoAssignAgent();
            anchor ??= transform;

            if (barRoot == null || fillImage == null)
            {
                BuildRuntimeUI();
            }

            ApplySize();
            ApplyScale();
            ApplyColors();
            ConfigureSlider();
        }

        void OnEnable()
        {
            TryAutoAssignAgent();
            RegisterAgent();
            ForceUpdateVisuals();
        }

        void OnDisable()
        {
            UnregisterAgent();
        }

        void OnDestroy()
        {
            UnregisterAgent();
        }

        void LateUpdate()
        {
            if (barRoot == null)
            {
                return;
            }

            UpdateTransform();
            UpdateFill(Time.deltaTime);
            UpdateVisibility(Time.deltaTime);
        }

        public void BindAgent(CharacterCombatAgent agent)
        {
            if (combatAgent == agent)
            {
                return;
            }

            UnregisterAgent();
            combatAgent = agent;
            RegisterAgent();
        }

        public void SetCamera(Camera camera)
        {
            cameraOverride = camera;
            if (canvas != null)
            {
                canvas.worldCamera = cameraOverride != null ? cameraOverride : Camera.main;
            }
        }

        void TryAutoAssignAgent()
        {
            if (combatAgent == null && autoBindAgent)
            {
                combatAgent = GetComponent<CharacterCombatAgent>();
            }
        }

        void RegisterAgent()
        {
            if (combatAgent == null || eventsHooked)
            {
                return;
            }

            combatAgent.HealthChanged += OnAgentHealthChanged;
            combatAgent.DamageTaken += OnAgentDamageTaken;
            combatAgent.Died += OnAgentDied;
            combatAgent.Revived += OnAgentRevived;
            eventsHooked = true;
            SyncFromAgent();
        }

        void UnregisterAgent(bool clearReference = true)
        {
            if (combatAgent != null && eventsHooked)
            {
                combatAgent.HealthChanged -= OnAgentHealthChanged;
                combatAgent.DamageTaken -= OnAgentDamageTaken;
                combatAgent.Died -= OnAgentDied;
                combatAgent.Revived -= OnAgentRevived;
            }

            eventsHooked = false;

            if (clearReference)
            {
                combatAgent = null;
            }
        }

        void OnAgentHealthChanged(float current, float max)
        {
            targetFill = Normalize(current, max);
            isDead = current <= 0f;
            visibleTimer = Mathf.Max(visibleTimer, visibilityDuration);
        }

        void OnAgentDamageTaken(DamageInfo damage, float current)
        {
            float max = combatAgent != null ? combatAgent.MaxHealth : current;
            targetFill = Normalize(current, max);
            isDead = current <= 0f;
            visibleTimer = visibilityDuration;
        }

        void OnAgentDied()
        {
            isDead = true;
            targetFill = 0f;
            visibleTimer = Mathf.Max(visibleTimer, visibilityDuration);
        }

        void OnAgentRevived()
        {
            isDead = false;
            float max = combatAgent != null ? combatAgent.MaxHealth : 1f;
            float current = combatAgent != null ? combatAgent.CurrentHealth : max;
            targetFill = Normalize(current, max);
            visibleTimer = Mathf.Max(visibleTimer, visibilityDuration);
        }

        void UpdateTransform()
        {
            Transform anchorTransform = Anchor;
            Vector3 position = anchorTransform.position + worldOffset;
            barRoot.position = position;

            if (faceCamera)
            {
                Camera cam = cameraOverride != null ? cameraOverride : Camera.main;
                if (cam != null)
                {
                    Vector3 toCamera = position - cam.transform.position;
                    if (toCamera.sqrMagnitude > 0.001f)
                    {
                        barRoot.rotation = Quaternion.LookRotation(toCamera);
                    }
                }
            }
            else
            {
                barRoot.rotation = anchorTransform.rotation;
            }

            barRoot.localScale = Vector3.one * uniformScale;
        }

        void UpdateFill(float deltaTime)
        {
            currentFill = Mathf.MoveTowards(currentFill, targetFill, deltaTime * fillLerpSpeed);
            trailingFill = Mathf.MoveTowards(trailingFill, targetFill, deltaTime * damageLerpSpeed);

            if (slider != null)
            {
                slider.value = currentFill;
            }

            if (fillImage != null)
            {
                fillImage.fillAmount = currentFill;
            }

            if (damageImage != null)
            {
                damageImage.fillAmount = trailingFill;
            }
        }

        void UpdateVisibility(float deltaTime)
        {
            if (canvasGroup == null)
            {
                return;
            }

            if (visibleTimer > 0f)
            {
                visibleTimer = Mathf.Max(visibleTimer - deltaTime, 0f);
            }

            bool shouldBeVisible = alwaysVisible || (!isDead && (targetFill < 1f || visibleTimer > 0f));
            float targetAlpha = shouldBeVisible ? 1f : 0f;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, deltaTime * fadeSpeed);
        }

        void ForceUpdateVisuals()
        {
            SyncFromAgent();
            currentFill = targetFill;
            trailingFill = targetFill;

            if (slider != null)
            {
                slider.value = targetFill;
            }

            if (fillImage != null)
            {
                fillImage.fillAmount = targetFill;
            }

            if (damageImage != null)
            {
                damageImage.fillAmount = targetFill;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = alwaysVisible || targetFill < 1f ? 1f : 0f;
            }
        }

        void BuildRuntimeUI()
        {
            var root = new GameObject(RuntimeRootName, typeof(RectTransform));
            root.transform.SetParent(transform, false);
            barRoot = root.GetComponent<RectTransform>();
            barRoot.localPosition = Vector3.zero;
            barRoot.localRotation = Quaternion.identity;

            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cameraOverride != null ? cameraOverride : Camera.main;
            canvas.sortingOrder = sortingOrder;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = alwaysVisible ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            slider = root.GetComponent<Slider>();
            if (slider == null)
            {
                slider = root.gameObject.AddComponent<Slider>();
            }

            backgroundImage = CreateLayer("Background", backgroundColor, Image.Type.Sliced);
            damageImage = CreateLayer("Damage", damageColor, Image.Type.Filled);
            damageImage.fillMethod = Image.FillMethod.Horizontal;
            damageImage.fillOrigin = (int)Image.OriginHorizontal.Left;

            fillImage = CreateLayer("Fill", fillColor, Image.Type.Filled);
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;

            ConfigureSlider();
        }

        Image CreateLayer(string name, Color color, Image.Type type)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(barRoot, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.color = color;
            image.type = type;
            image.raycastTarget = false;

            if (defaultSprite == null)
            {
                defaultSprite = CreateSolidSprite(ref defaultTexture, "NpcHealthBar_DefaultSprite");
            }

            if (image.sprite == null && defaultSprite != null)
            {
                image.sprite = defaultSprite;
            }

            return image;
        }

        static Sprite CreateSolidSprite(ref Texture2D texture, string textureName)
        {
            if (texture == null)
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = textureName,
                    hideFlags = HideFlags.HideAndDontSave
                };
                var pixels = new Color[4];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = Color.white;
                }
                texture.SetPixels(pixels);
                texture.Apply();
            }

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            sprite.name = textureName + "_Sprite";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        void ConfigureSlider()
        {
            if (slider == null)
            {
                return;
            }

            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };

            if (fillImage != null)
            {
                slider.fillRect = fillImage.rectTransform;
                slider.targetGraphic = fillImage;
            }

            slider.value = targetFill;
        }

        void ApplySize()
        {
            if (barRoot != null)
            {
                barRoot.sizeDelta = barSize;
            }
        }

        void ApplyScale()
        {
            if (barRoot != null)
            {
                barRoot.localScale = Vector3.one * uniformScale;
            }
        }

        void ApplyColors()
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = backgroundColor;
            }

            if (fillImage != null)
            {
                fillImage.color = fillColor;
            }

            if (damageImage != null)
            {
                damageImage.color = damageColor;
            }
        }

        void SyncFromAgent()
        {
            if (combatAgent == null)
            {
                targetFill = 0f;
                return;
            }

            float max = combatAgent.MaxHealth;
            float current = Mathf.Clamp(combatAgent.CurrentHealth, 0f, max);
            targetFill = Normalize(current, max);
            isDead = !combatAgent.IsAlive;
        }

        static float Normalize(float current, float max)
        {
            return max > 0f ? Mathf.Clamp01(current / max) : 0f;
        }
    }
}
