using System;
using UnityEngine;
using UnityEngine.Events;

namespace HeroCharacter
{
    /// <summary>
    /// Reusable combat brain that tracks health, damage, blocking, and attack cadence for hero or NPC characters.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterCombatAgent : MonoBehaviour, IDamageable
    {
        [Serializable]
        public class Settings
        {
            [Min(1f)] public float maxHealth = 100f;
            [Range(0f, 1f)] public float startingHealthFraction = 1f;
            [Min(0f)] public float attackDamage = 10f;
            [Range(0f, 1f)] public float attackDamageVariance = 0.3f;
            [Min(0f)] public float attackCooldown = 0.6f;
            [Tooltip("Incoming damage multiplier while blocking (0 = full negate, 1 = no mitigation).")]
            [Range(0f, 1f)] public float blockDamageMultiplier = 0.4f;
            [Tooltip("Minimum time in seconds after taking damage before new damage is accepted.")]
            [Min(0f)] public float postHitInvulnerability = 0.15f;
            public DamageType defaultAttackDamageType = DamageType.Physical;
        }

        [Header("Settings")]
        [SerializeField] Settings combatSettings = new Settings();

        [Header("HUD")]
        [SerializeField] MonoBehaviour hudProvider;
        [SerializeField] bool autoFindHudInChildren = true;

        [Header("Events")]
        [SerializeField] HealthChangedUnityEvent onHealthChanged = new HealthChangedUnityEvent();
        [SerializeField] DamageInfoUnityEvent onDamageTaken = new DamageInfoUnityEvent();
        [SerializeField] UnityEvent onDeath = new UnityEvent();
        [SerializeField] UnityEvent onRevived = new UnityEvent();
        [SerializeField] UnityEvent onAttackStarted = new UnityEvent();
        [SerializeField] UnityEvent onAttackPerformed = new UnityEvent();
        [SerializeField] BoolUnityEvent onBlockStateChanged = new BoolUnityEvent();

        float currentHealth;
        float lastAttackTime = float.NegativeInfinity;
        float lastDamageTime = float.NegativeInfinity;
        bool isBlocking;
        bool isAlive = true;

        ICombatHUD hud;

        public Settings CombatSettings => combatSettings;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => combatSettings.maxHealth;
        public bool IsBlocking => isBlocking;
        public bool IsAlive => isAlive;
        public bool CanAttack => isAlive && Time.time >= (lastAttackTime + combatSettings.attackCooldown);

        public event Action<float, float> HealthChanged;
        public event Action<DamageInfo, float> DamageTaken;
        public event Action Died;
        public event Action Revived;
        public event Action AttackStarted;
        public event Action AttackPerformed;
        public event Action<bool> BlockStateChanged;

        void Reset()
        {
            CacheHud();
            ResetHealthInternal();
        }

        void Awake()
        {
            CacheHud();
            ResetHealthInternal();
        }

        void OnValidate()
        {
            combatSettings.maxHealth = Mathf.Max(1f, combatSettings.maxHealth);
            combatSettings.attackDamage = Mathf.Max(0f, combatSettings.attackDamage);
            combatSettings.attackCooldown = Mathf.Max(0f, combatSettings.attackCooldown);
            combatSettings.postHitInvulnerability = Mathf.Max(0f, combatSettings.postHitInvulnerability);
            combatSettings.attackDamageVariance = Mathf.Clamp01(combatSettings.attackDamageVariance);
            cacheHudDelayed = true;
        }

        bool cacheHudDelayed;

        void Start()
        {
            if (cacheHudDelayed)
            {
                CacheHud();
                cacheHudDelayed = false;
            }

            RaiseHealthChanged();
            if (isBlocking)
            {
                RaiseBlockChanged();
            }
        }

        /// <summary>
        /// Try to trigger a new attack; returns false if blocked by cooldown or the agent is dead.
        /// </summary>
        public bool TryStartAttack()
        {
            if (!IsAlive)
            {
                return false;
            }

            if (!CanAttack)
            {
                return false;
            }

            lastAttackTime = Time.time;
            onAttackStarted.Invoke();
            AttackStarted?.Invoke();
            return true;
        }

        /// <summary>
        /// Notifies the combat agent that the attack animation hit window fired and damage should be applied.
        /// </summary>
        public bool ResolveAttackHit(IDamageable target, Vector3 hitPoint, Vector3 hitNormal, float damageScale = 1f, bool unblockable = false)
        {
            if (!IsAlive)
            {
                return false;
            }

            onAttackPerformed.Invoke();
            AttackPerformed?.Invoke();

            if (target == null || !target.IsAlive)
            {
                return false;
            }

            float amount = combatSettings.attackDamage * Mathf.Max(0f, damageScale);
            amount = ApplyDamageVariance(amount);
            var damage = new DamageInfo(
                amount,
                combatSettings.defaultAttackDamageType,
                gameObject,
                gameObject,
                hitPoint,
                hitNormal,
                unblockable);
            target.ApplyDamage(damage);
            return true;
        }

        /// <summary>
        /// Apply chip damage or no damage when attack misses but still needs to signal completion.
        /// </summary>
        public void NotifyAttackAnimationComplete()
        {
            onAttackPerformed.Invoke();
            AttackPerformed?.Invoke();
        }

        public void SetBlocking(bool active)
        {
            if (isBlocking == active)
            {
                return;
            }

            isBlocking = active;
            RaiseBlockChanged();

        }

        public void ApplyDamage(DamageInfo damage)
        {
            if (!IsAlive)
            {
                return;
            }

            float timeSinceLastDamage = Time.time - lastDamageTime;
            if (combatSettings.postHitInvulnerability > 0f && timeSinceLastDamage < combatSettings.postHitInvulnerability && !damage.unblockable)
            {
                return;
            }

            float mitigatedAmount = damage.amount;
            if (isBlocking && !damage.unblockable)
            {
                mitigatedAmount *= combatSettings.blockDamageMultiplier;
            }

            mitigatedAmount = Mathf.Max(0f, mitigatedAmount);
            if (mitigatedAmount <= 0f)
            {
                return;
            }

            lastDamageTime = Time.time;

            damage.amount = mitigatedAmount;
            currentHealth = Mathf.Max(0f, currentHealth - mitigatedAmount);
            onDamageTaken.Invoke(damage);
            DamageTaken?.Invoke(damage, currentHealth);
            RaiseHealthChanged();
            hud?.HandleDamageTaken(damage, currentHealth, combatSettings.maxHealth);

            if (currentHealth <= 0f)
            {
                HandleDeath();
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            float prevHealth = currentHealth;
            currentHealth = Mathf.Min(combatSettings.maxHealth, currentHealth + amount);
            if (!Mathf.Approximately(prevHealth, currentHealth))
            {
                RaiseHealthChanged();
            }
        }

        public void Revive(float healthFraction = 1f)
        {
            float clampedFraction = Mathf.Clamp01(healthFraction);
            currentHealth = Mathf.Max(1f, combatSettings.maxHealth * clampedFraction);
            isAlive = true;
            onRevived.Invoke();
            Revived?.Invoke();
            RaiseHealthChanged();
            hud?.HandleRevive(currentHealth, combatSettings.maxHealth);
            if (isBlocking)
            {
                RaiseBlockChanged();
            }
        }

        public void ResetHealth()
        {
            ResetHealthInternal();
            RaiseHealthChanged();
        }

        public DamageInfo CreateDamageInfo(float damageScale = 1f, bool unblockable = false)
        {
            float amount = combatSettings.attackDamage * Mathf.Max(0f, damageScale);
            amount = ApplyDamageVariance(amount);
            return new DamageInfo(
                amount,
                combatSettings.defaultAttackDamageType,
                gameObject,
                gameObject,
                transform.position,
                Vector3.up,
                unblockable);
        }

        public void AttachHud(ICombatHUD hudInstance)
        {
            hud = hudInstance;
            if (hud != null)
            {
                hud.HandleHealthChanged(currentHealth, combatSettings.maxHealth);
                hud.HandleBlockState(isBlocking);
                if (!isAlive)
                {
                    hud.HandleDeath();
                }
            }
        }

        public void DetachHud(ICombatHUD hudInstance)
        {
            if (hud == hudInstance)
            {
                hud = null;
            }
        }

        void HandleDeath()
        {
            if (!isAlive)
            {
                return;
            }

            isAlive = false;
            onDeath.Invoke();
            Died?.Invoke();
            hud?.HandleDeath();
        }

        void ResetHealthInternal()
        {
            currentHealth = combatSettings.maxHealth * Mathf.Clamp01(combatSettings.startingHealthFraction);
            if (Mathf.Approximately(currentHealth, 0f))
            {
                currentHealth = combatSettings.maxHealth;
            }
            isAlive = currentHealth > 0f;
        }

        void CacheHud()
        {
            if (hudProvider != null)
            {
                hud = hudProvider as ICombatHUD ?? hudProvider.GetComponent<ICombatHUD>();
                if (hud == null)
                {
                    Debug.LogWarning($"HUD provider on {name} does not implement ICombatHUD.", this);
                }
            }

            if (hud == null && autoFindHudInChildren)
            {
                hud = GetComponentInChildren<ICombatHUD>(true);
            }
        }

        void RaiseHealthChanged()
        {
            onHealthChanged.Invoke(currentHealth, combatSettings.maxHealth);
            HealthChanged?.Invoke(currentHealth, combatSettings.maxHealth);
            hud?.HandleHealthChanged(currentHealth, combatSettings.maxHealth);
        }

        void RaiseBlockChanged()
        {
            onBlockStateChanged.Invoke(isBlocking);
            BlockStateChanged?.Invoke(isBlocking);
            hud?.HandleBlockState(isBlocking);
        }

        float ApplyDamageVariance(float amount)
        {
            if (amount <= 0f)
            {
                return 0f;
            }

            float variance = Mathf.Clamp01(combatSettings.attackDamageVariance);
            if (variance <= 0f)
            {
                return amount;
            }

            float scale = UnityEngine.Random.Range(1f - variance, 1f + variance);
            return Mathf.Max(0f, amount * scale);
        }

        void OnDisable()
        {
            if (hud != null && autoFindHudInChildren)
            {
                hud.HandleHealthChanged(currentHealth, combatSettings.maxHealth);
                hud.HandleBlockState(isBlocking);
                if (!isAlive)
                {
                    hud.HandleDeath();
                }
            }
        }
    }
}
