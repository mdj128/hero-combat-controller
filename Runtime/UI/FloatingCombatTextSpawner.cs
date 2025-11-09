using UnityEngine;
using Random = UnityEngine.Random;

namespace HeroCharacter
{
    /// <summary>
    /// Subscribes to CharacterCombatAgent damage events and spawns floating numbers above the actor.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterCombatAgent))]
    public class FloatingCombatTextSpawner : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] Transform anchor;
        [SerializeField] Vector3 spawnOffset = new Vector3(0f, 2f, 0f);

        [Header("Visuals")]
        [SerializeField] bool faceCamera = true;
        [SerializeField] Camera cameraOverride;
        [SerializeField] Color lightDamageColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        [SerializeField] Color heavyDamageColor = new Color(0.6f, 0.05f, 0.05f, 1f);
        [SerializeField, Min(0f)] float heavyDamageThreshold = 40f;
        [SerializeField] float sizeJitter = 0.1f;

        [Header("Animation")]
        [SerializeField, Min(0.1f)] float lifetime = 1.4f;
        [SerializeField] float riseSpeed = 1.5f;
        [SerializeField] Vector2 horizontalJitter = new Vector2(-0.2f, 0.2f);
        [SerializeField] Vector2 verticalJitter = new Vector2(0f, 0.2f);

        [Header("Prefab (optional)")]
        [SerializeField] FloatingCombatText prefab;
        [SerializeField] Font fontOverride;

        CharacterCombatAgent combatAgent;

        public Transform Anchor
        {
            get => anchor;
            set => anchor = value;
        }

        public Vector3 SpawnOffset
        {
            get => spawnOffset;
            set => spawnOffset = value;
        }

        public Color DamageColor
        {
            get => lightDamageColor;
            set
            {
                lightDamageColor = value;
                heavyDamageColor = value;
            }
        }

        public Camera CameraOverride
        {
            get => cameraOverride;
            set => cameraOverride = value;
        }

        void Awake()
        {
            combatAgent = GetComponent<CharacterCombatAgent>();
            anchor ??= transform;
        }

        void OnEnable()
        {
            if (combatAgent != null)
            {
                combatAgent.DamageTaken += OnDamageTaken;
            }
        }

        void OnDisable()
        {
            if (combatAgent != null)
            {
                combatAgent.DamageTaken -= OnDamageTaken;
            }
        }

        void OnDamageTaken(DamageInfo damage, float currentHealth)
        {
            if (damage.amount <= 0f)
            {
                return;
            }

            var instance = CreateInstance();
            if (instance == null)
            {
                return;
            }

            float displayAmount = Mathf.Ceil(damage.amount);
            string text = Mathf.Approximately(displayAmount, Mathf.Floor(displayAmount))
                ? displayAmount.ToString("0")
                : damage.amount.ToString("0.0");

            Vector3 basePosition = (anchor != null ? anchor.position : transform.position) + spawnOffset;
            basePosition += new Vector3(
                Random.Range(horizontalJitter.x, horizontalJitter.y),
                Random.Range(verticalJitter.x, verticalJitter.y),
                Random.Range(horizontalJitter.x, horizontalJitter.y));

            float sizeMultiplier = 1f + Random.Range(-sizeJitter, sizeJitter);
            Color color = SelectColor(damage.amount);
            var cam = cameraOverride != null ? cameraOverride : Camera.main;
            instance.Play(
                text,
                basePosition,
                color,
                lifetime,
                riseSpeed,
                horizontalJitter,
                verticalJitter,
                cam,
                faceCamera,
                sizeMultiplier);
        }

        FloatingCombatText CreateInstance()
        {
            if (prefab != null)
            {
                return Instantiate(prefab);
            }

            var font = FloatingCombatText.FindDefaultFont(fontOverride);
            return FloatingCombatText.CreateRuntimeInstance(font);
        }

        Color SelectColor(float amount)
        {
            if (heavyDamageThreshold <= 0f)
            {
                return lightDamageColor;
            }

            float normalized = Mathf.Clamp01(amount / heavyDamageThreshold);
            return Color.Lerp(lightDamageColor, heavyDamageColor, normalized);
        }
    }
}
