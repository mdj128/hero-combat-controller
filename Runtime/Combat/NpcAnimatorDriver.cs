using UnityEngine;

namespace HeroCharacter
{
    /// <summary>
    /// Small helper that mirrors the hero controller's animator parameter updates for NPCs.
    /// Keeps reuse simple: copy the hero animator controller to the NPC and let this driver feed the same parameters.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterCombatAgent))]
    public class NpcAnimatorDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Animator animator;
        [SerializeField] Rigidbody body;

        [Header("Parameter Names")]
        [SerializeField] string speedFloat = "Vel";
        [SerializeField] string idleBool = "Idle";
        [SerializeField] string groundedBool = "Grounded";
        [SerializeField] string sprintBool = "Sprinting";
        [SerializeField] string attackTrigger = "Attacking";
        [SerializeField] string damageTrigger = "Damage";
        [SerializeField] string deathTrigger = "Death";

        [Header("Tuning")]
        [SerializeField] float idleThreshold = 0.05f;
        [SerializeField] float sprintThreshold = 3.5f;
        [SerializeField] bool assumeGrounded = true;

        CharacterCombatAgent combatAgent;
        Vector3 lastPosition;
        bool subscribed;

        void Reset()
        {
            CacheReferences();
        }

        void Awake()
        {
            CacheReferences();
        }

        void OnEnable()
        {
            CacheReferences();
            lastPosition = transform.position;
            SubscribeCombatEvents(true);
        }

        void OnDisable()
        {
            SubscribeCombatEvents(false);
        }

        void Update()
        {
            if (animator == null)
            {
                return;
            }

            Vector3 velocity = GetWorldVelocity();
            float planarSpeed = new Vector2(velocity.x, velocity.z).magnitude;

            if (!string.IsNullOrEmpty(speedFloat))
            {
                animator.SetFloat(speedFloat, planarSpeed);
            }

            if (!string.IsNullOrEmpty(idleBool))
            {
                animator.SetBool(idleBool, planarSpeed <= idleThreshold);
            }

            if (!string.IsNullOrEmpty(groundedBool))
            {
                animator.SetBool(groundedBool, assumeGrounded);
            }

            if (!string.IsNullOrEmpty(sprintBool))
            {
                animator.SetBool(sprintBool, planarSpeed >= sprintThreshold);
            }

            lastPosition = transform.position;
        }

        void HandleAttackStarted()
        {
            if (!string.IsNullOrEmpty(attackTrigger))
            {
                animator.SetTrigger(attackTrigger);
            }
        }

        void CacheReferences()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (combatAgent == null)
            {
                combatAgent = GetComponent<CharacterCombatAgent>();
            }

            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }
        }

        void SubscribeCombatEvents(bool subscribe)
        {
            if (combatAgent == null)
            {
                return;
            }

            if (subscribe && !subscribed)
            {
                combatAgent.AttackStarted += HandleAttackStarted;
                combatAgent.AttackStarted += HandleAttackStarted;
                combatAgent.DamageTaken += HandleDamageTaken;
                combatAgent.Died += HandleDied;
                combatAgent.Revived += HandleRevived;
                subscribed = true;
            }
            else if (!subscribe && subscribed)
            {
                combatAgent.AttackStarted -= HandleAttackStarted;
                combatAgent.DamageTaken -= HandleDamageTaken;
                combatAgent.Died -= HandleDied;
                combatAgent.Revived -= HandleRevived;
                subscribed = false;
            }
        }

        Vector3 GetWorldVelocity()
        {
            if (body != null)
            {
#if UNITY_6000_0_OR_NEWER
                return body.linearVelocity;
#else
                return body.velocity;
#endif
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= Mathf.Epsilon)
            {
                return Vector3.zero;
            }

            return (transform.position - lastPosition) / deltaTime;
        }

        void HandleDamageTaken(DamageInfo damage, float currentHealth)
        {
            if (!string.IsNullOrEmpty(damageTrigger))
            {
                animator.SetTrigger(damageTrigger);
            }
        }

        void HandleDied()
        {
            if (!string.IsNullOrEmpty(deathTrigger))
            {
                animator.SetTrigger(deathTrigger);
            }
        }

        void HandleRevived()
        {
            // reset animator state if needed
        }
    }
}
