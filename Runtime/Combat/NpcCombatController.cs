using UnityEngine;

namespace HeroCharacter
{
    /// <summary>
    /// Minimal NPC combat brain that reuses CharacterCombatAgent and targets IDamageable actors.
    /// Acts as a scaffold for future enemy behaviour.
    /// </summary>
    [RequireComponent(typeof(CharacterCombatAgent))]
    public class NpcCombatController : MonoBehaviour
    {
        [Header("Targeting")]
        [SerializeField] bool autoTargetHero = true;
        [SerializeField] HeroCharacterController heroTargetOverride;
        [SerializeField] float acquireRange = 5f;
        [SerializeField] float attackRange = 1.9f;
        [SerializeField] float turnSpeed = 8f;
        [SerializeField] float leashRange = 10f;
        [SerializeField] float returnSpeed = 3f;

        [Header("Movement")]
        [SerializeField] bool enableMovement = true;
        [SerializeField] bool allowSprint = true;
        [SerializeField] float walkSpeed = 1.5f;
        [SerializeField] float sprintSpeed = 3f;
        [SerializeField] float sprintDistance = 3f;
        [SerializeField] float stoppingBuffer = 0.35f;

        [Header("Damage")]
        [SerializeField] float damageScale = 1f;
        [SerializeField] float attackDelay = 1f;

        [Header("UI")]
        [SerializeField] bool attachHealthBar = true;
        [SerializeField] Vector3 healthBarOffset = new Vector3(0f, 1.6f, 0f);
        [SerializeField] Vector2 healthBarSize = new Vector2(1.2f, 0.18f);
        [SerializeField, Min(0.01f)] float healthBarScale = 1f;
        [SerializeField] bool healthBarAlwaysVisible;
        [SerializeField, HideInInspector] int healthBarConfigVersion;

        [Header("Feedback")]
        [SerializeField] bool attachFloatingText = true;
        [SerializeField] Vector3 floatingTextOffset = new Vector3(0f, 2.1f, 0f);
        [SerializeField] Color floatingTextColor = new Color(0.235f, 0.776f, 0.851f, 1f);

        const int CurrentHealthBarConfigVersion = 1;

        CharacterCombatAgent combatAgent;
        IDamageable currentTarget;
        Transform targetTransform;
        Vector3 spawnPosition;
        bool returningToSpawn;
        float lastAttackTime = float.NegativeInfinity;
        NpcWorldspaceHealthBar healthBar;
        FloatingCombatTextSpawner floatingText;
        Vector3 lastTargetPosition;

        void Awake()
        {
            combatAgent = GetComponent<CharacterCombatAgent>();
            spawnPosition = transform.position;
            EnsureHealthBar();
            EnsureFloatingText();
        }

        void OnEnable()
        {
            spawnPosition = transform.position;
            AcquireTargetIfNeeded();
            EnsureHealthBar();
            EnsureFloatingText();
        }

        void OnValidate()
        {
            if (healthBarConfigVersion < CurrentHealthBarConfigVersion)
            {
                attachHealthBar = true;
                healthBarConfigVersion = CurrentHealthBarConfigVersion;
            }

            healthBarScale = Mathf.Max(0.01f, healthBarScale);
            healthBarSize.x = Mathf.Max(0.01f, healthBarSize.x);
            healthBarSize.y = Mathf.Max(0.01f, healthBarSize.y);
            EnsureFloatingText();
            ConfigureHealthBar(healthBar);
            ConfigureFloatingText(floatingText);
        }

        void Update()
        {
            if (combatAgent == null || !combatAgent.IsAlive)
            {
                return;
            }

            if (!EnsureTarget())
            {
                return;
            }

            Vector3 toTarget = targetTransform.position - transform.position;
            float sqrDistance = toTarget.sqrMagnitude;
            if (sqrDistance > leashRange * leashRange)
            {
                ClearTarget();
                returningToSpawn = true;
            }

            if (returningToSpawn)
            {
                ReturnToSpawn();
                return;
            }

            Vector3 flatDirection = new Vector3(toTarget.x, 0f, toTarget.z);
            if (flatDirection.sqrMagnitude > Mathf.Epsilon)
            {
                Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
            }

            if (enableMovement && sqrDistance > Mathf.Epsilon)
            {
                float distance = Mathf.Sqrt(sqrDistance);
                float desiredStopDistance = Mathf.Max(attackRange - stoppingBuffer, 0.8f);
                if (distance > desiredStopDistance)
                {
                    Vector3 targetPos = targetTransform.position;
                    if ((targetPos - lastTargetPosition).sqrMagnitude < 0.0001f)
                    {
                        targetPos = lastTargetPosition;
                    }
                    else
                    {
                        lastTargetPosition = targetPos;
                    }
                    Vector3 moveDir = flatDirection.normalized;
                    float speed = walkSpeed;
                    if (allowSprint && distance > sprintDistance)
                    {
                        speed = sprintSpeed;
                    }
                    float remaining = distance - desiredStopDistance;
                    Vector3 desiredPosition = targetPos - moveDir * desiredStopDistance;
                    Vector3 newPosition = Vector3.MoveTowards(transform.position, desiredPosition, speed * Time.deltaTime);
                    transform.position = new Vector3(newPosition.x, transform.position.y, newPosition.z);
                    // refresh distance for attack check so we don't instantly stop before entering range
                    toTarget = targetTransform.position - transform.position;
                    sqrDistance = toTarget.sqrMagnitude;
                }
                else if (distance < desiredStopDistance - 0.05f)
                {
                    Vector3 moveDir = -flatDirection.normalized;
                    float step = Mathf.Min(walkSpeed * Time.deltaTime, desiredStopDistance - distance);
                    transform.position += moveDir * step;
                }
            }

            if (sqrDistance <= attackRange * attackRange)
            {
                if (Time.time >= lastAttackTime + attackDelay)
                {
                    if (combatAgent.TryStartAttack())
                    {
                        lastAttackTime = Time.time;
                    }
                }
            }
        }

        public void SetTarget(IDamageable damageable)
        {
            currentTarget = damageable;
            if (damageable is Component component)
            {
                targetTransform = component.transform;
            }
            else
            {
                targetTransform = null;
            }
            returningToSpawn = false;
        }

        /// <summary>
        /// Called by animation events when the NPC's weapon connects.
        /// </summary>
        public void NotifyAttackHit(Vector3 hitPoint, Vector3 hitNormal)
        {
            if (combatAgent == null)
            {
                return;
            }

            if (currentTarget == null || !currentTarget.IsAlive)
            {
                combatAgent.NotifyAttackAnimationComplete();
                return;
            }

            combatAgent.ResolveAttackHit(currentTarget, hitPoint, hitNormal, damageScale);
        }

        bool EnsureTarget()
        {
            if (currentTarget != null && currentTarget.IsAlive && targetTransform != null)
            {
                float sqrDistance = (targetTransform.position - transform.position).sqrMagnitude;
                if (sqrDistance > leashRange * leashRange)
                {
                    ClearTarget();
                    returningToSpawn = true;
                    return false;
                }
                return true;
            }

            AcquireTargetIfNeeded();
            return currentTarget != null && currentTarget.IsAlive && targetTransform != null;
        }

        void AcquireTargetIfNeeded()
        {
            if (heroTargetOverride != null && heroTargetOverride.IsAlive)
            {
                SetTarget(heroTargetOverride);
                return;
            }

            if (!autoTargetHero)
            {
                return;
            }

            if (currentTarget != null && currentTarget.IsAlive)
            {
                return;
            }

            var hero = FindHero();
            if (hero != null)
            {
                SetTarget(hero);
            }
#if UNITY_EDITOR
            Gizmos.color = new Color(1f, 0f, 1f, 0.4f);
            Gizmos.DrawWireSphere(spawnPosition == Vector3.zero ? transform.position : spawnPosition, leashRange);
#endif
        }

        void ClearTarget()
        {
            currentTarget = null;
            targetTransform = null;
            lastAttackTime = float.NegativeInfinity;
        }

        void EnsureHealthBar()
        {
            if (!attachHealthBar)
            {
                if (healthBar != null)
                {
                    healthBar.gameObject.SetActive(false);
                }
                return;
            }

            if (combatAgent == null)
            {
                return;
            }

            if (healthBar == null)
            {
                healthBar = GetComponentInChildren<NpcWorldspaceHealthBar>(true);
            }

            if (healthBar == null)
            {
                healthBar = gameObject.AddComponent<NpcWorldspaceHealthBar>();
            }

            if (!healthBar.gameObject.activeSelf)
            {
                healthBar.gameObject.SetActive(true);
            }

            ConfigureHealthBar(healthBar);
        }

        void ConfigureHealthBar(NpcWorldspaceHealthBar bar)
        {
            if (bar == null)
            {
                return;
            }

            bar.Anchor = transform;
            bar.WorldOffset = healthBarOffset;
            bar.BarSize = healthBarSize;
            bar.UniformScale = healthBarScale;
            bar.AlwaysVisible = healthBarAlwaysVisible;
            bar.BindAgent(combatAgent);
        }

        void EnsureFloatingText()
        {
            if (!attachFloatingText)
            {
                if (floatingText != null)
                {
                    floatingText.enabled = false;
                }
                return;
            }

            if (floatingText == null)
            {
                floatingText = GetComponent<FloatingCombatTextSpawner>();
            }

            if (floatingText == null)
            {
                floatingText = gameObject.AddComponent<FloatingCombatTextSpawner>();
            }

            ConfigureFloatingText(floatingText);
        }

        void ConfigureFloatingText(FloatingCombatTextSpawner spawner)
        {
            if (spawner == null)
            {
                return;
            }

            spawner.enabled = true;
            spawner.Anchor = transform;
            spawner.SpawnOffset = floatingTextOffset;
            spawner.DamageColor = floatingTextColor;
        }

        void ReturnToSpawn()
        {
            Vector3 toSpawn = spawnPosition - transform.position;
            float sqrDistance = toSpawn.sqrMagnitude;
            if (sqrDistance <= 0.01f)
            {
                returningToSpawn = false;
                transform.position = spawnPosition;
                transform.rotation = Quaternion.identity;
                return;
            }

            Vector3 flat = new Vector3(toSpawn.x, 0f, toSpawn.z);
            if (flat.sqrMagnitude > Mathf.Epsilon)
            {
                Quaternion targetRotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
            }

            float distance = Mathf.Sqrt(sqrDistance);
            float step = returnSpeed * Time.deltaTime;
            if (step >= distance)
            {
                transform.position = spawnPosition;
                returningToSpawn = false;
                return;
            }

            transform.position += flat.normalized * step;
        }

#if UNITY_6000_0_OR_NEWER
        static HeroCharacterController FindHero()
        {
            return FindFirstObjectByType<HeroCharacterController>();
        }
#else
        static HeroCharacterController FindHero()
        {
            return FindObjectOfType<HeroCharacterController>();
        }
#endif

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, acquireRange);
#if UNITY_EDITOR
            Gizmos.color = new Color(1f, 0f, 1f, 0.4f);
            Vector3 leashCenter = Application.isPlaying ? spawnPosition : transform.position;
            Gizmos.DrawWireSphere(leashCenter, leashRange);
#endif
        }
    }
}
