using UnityEngine;
using UnityEngine.UI;

namespace HeroCharacter
{
    /// <summary>
    /// Simple HUD binder that listens to CharacterCombatAgent events and reflects them on UI elements.
    /// </summary>
    public class HeroCombatHudBinder : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] HeroCharacterController hero;
        [SerializeField] Slider healthSlider;
        [SerializeField] Image healthFillImage;
        [SerializeField] Text healthLabel;
        [SerializeField] GameObject deathOverlay;
        [SerializeField] GameObject blockIndicator;
        [SerializeField] Animator damageFeedbackAnimator;
        [SerializeField] string damageTrigger = "Damage";
        [SerializeField] bool autoFindHero = true;

        CharacterCombatAgent combatAgent;
        bool eventsHooked;

        static Sprite defaultFillSprite;
        static Texture2D defaultFillTexture;

        void Awake()
        {
            EnsureFillSprite();
            EnsureSlider();
            ResolveHeroReference();
        }

        void OnEnable()
        {
            ResolveHeroReference();
            Register();
        }

        void OnDisable()
        {
            Unregister();
        }

        void HandleHealthChanged(float current, float max)
        {
            float normalized = max > 0f ? current / max : 0f;
            if (healthSlider != null)
            {
                float value = Mathf.Lerp(healthSlider.minValue, healthSlider.maxValue, normalized);
                healthSlider.value = value;
            }
            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = normalized;
            }

            if (healthLabel != null)
            {
                healthLabel.text = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
            }
        }

        void HandleDamageTaken(DamageInfo damage, float current, float max)
        {
            if (damageFeedbackAnimator != null && !string.IsNullOrEmpty(damageTrigger))
            {
                damageFeedbackAnimator.ResetTrigger(damageTrigger);
                damageFeedbackAnimator.SetTrigger(damageTrigger);
            }
        }

        void HandleBlockState(bool isBlocking)
        {
            if (blockIndicator != null)
            {
                blockIndicator.SetActive(isBlocking);
            }
        }

        void HandleDeath()
        {
            if (deathOverlay != null)
            {
                deathOverlay.SetActive(true);
            }
        }

        void HandleRevive(float current, float max)
        {
            if (deathOverlay != null)
            {
                deathOverlay.SetActive(false);
            }
            HandleHealthChanged(current, max);
        }

        void ResolveHeroReference()
        {
            if (hero == null && autoFindHero)
            {
                hero = FindHeroController();
            }
        }

        void Register()
        {
            if (hero == null)
            {
                return;
            }

            if (!hero.TryGetComponent(out CharacterCombatAgent agent))
            {
                return;
            }

            Unregister();
            combatAgent = agent;
            RegisterAgentListeners();
        }

#if UNITY_6000_0_OR_NEWER
        static HeroCharacterController FindHeroController()
        {
            return FindFirstObjectByType<HeroCharacterController>();
        }
#else
        static HeroCharacterController FindHeroController()
        {
            return FindObjectOfType<HeroCharacterController>();
        }
#endif

        void RegisterAgentListeners()
        {
            if (combatAgent == null || eventsHooked)
            {
                return;
            }

            combatAgent.HealthChanged += OnAgentHealthChanged;
            combatAgent.DamageTaken += OnAgentDamageTaken;
            combatAgent.BlockStateChanged += OnAgentBlockStateChanged;
            combatAgent.Died += OnAgentDied;
            combatAgent.Revived += OnAgentRevived;
            eventsHooked = true;

            HandleHealthChanged(combatAgent.CurrentHealth, combatAgent.MaxHealth);
            HandleBlockState(combatAgent.IsBlocking);
            if (!combatAgent.IsAlive)
            {
                HandleDeath();
            }
        }

        void Unregister()
        {
            if (combatAgent != null && eventsHooked)
            {
                combatAgent.HealthChanged -= OnAgentHealthChanged;
                combatAgent.DamageTaken -= OnAgentDamageTaken;
                combatAgent.BlockStateChanged -= OnAgentBlockStateChanged;
                combatAgent.Died -= OnAgentDied;
                combatAgent.Revived -= OnAgentRevived;
            }

            eventsHooked = false;
            combatAgent = null;
        }

        void OnAgentHealthChanged(float current, float max)
        {
            HandleHealthChanged(current, max);
        }

        void OnAgentDamageTaken(DamageInfo damage, float current)
        {
            HandleDamageTaken(damage, current, combatAgent != null ? combatAgent.MaxHealth : current);
        }

        void OnAgentBlockStateChanged(bool isBlocking)
        {
            HandleBlockState(isBlocking);
        }

        void OnAgentDied()
        {
            HandleDeath();
        }

        void OnAgentRevived()
        {
            if (combatAgent == null)
            {
                return;
            }

            HandleRevive(combatAgent.CurrentHealth, combatAgent.MaxHealth);
        }

        void EnsureFillSprite()
        {
            if (healthFillImage == null)
            {
                return;
            }

            if (defaultFillSprite == null)
            {
                defaultFillSprite = CreateSolidSprite(ref defaultFillTexture, "HeroHUD_DefaultFillSprite");
            }

            if (healthFillImage.sprite == null && defaultFillSprite != null)
            {
                healthFillImage.sprite = defaultFillSprite;
            }

            if (healthFillImage.type != Image.Type.Filled)
            {
                healthFillImage.type = Image.Type.Filled;
            }

            healthFillImage.fillMethod = Image.FillMethod.Horizontal;
            healthFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        void EnsureSlider()
        {
            if (healthSlider == null)
            {
                healthSlider = GetComponentInChildren<Slider>(true);
            }

            if (healthSlider == null && healthFillImage != null)
            {
                RectTransform panel = healthFillImage.rectTransform.parent as RectTransform;
                if (panel == null)
                {
                    panel = healthFillImage.rectTransform;
                }

                healthSlider = panel.GetComponent<Slider>();
                if (healthSlider == null)
                {
                    healthSlider = panel.gameObject.AddComponent<Slider>();
                }
            }

            ConfigureSlider(healthSlider);
        }

        void ConfigureSlider(Slider slider)
        {
            if (slider == null)
            {
                return;
            }

            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            if (slider.fillRect == null && healthFillImage != null)
            {
                slider.fillRect = healthFillImage.rectTransform;
            }

            if (slider.targetGraphic == null && healthFillImage != null)
            {
                slider.targetGraphic = healthFillImage;
            }
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
    }
}
