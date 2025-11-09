using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace HeroCharacter
{
    /// <summary>
    /// Simple melee weapon helper that queries nearby IDamageable targets when an attack animation fires.
    /// </summary>
    [MovedFrom("HeroCharacter.HeroMeleeWeapon")]
    public class MeleeWeapon : MonoBehaviour
    {
        [SerializeField] CharacterCombatAgent owner;
        [SerializeField] Transform ownerTransform;
        [SerializeField] Transform attackOrigin;
        [SerializeField] float attackRadius = 2f;
        [SerializeField] float attackAngle = 120f;
        [SerializeField] LayerMask hitMask = ~0;
        [SerializeField] float damageMultiplier = 1f;
        [SerializeField] bool includeTriggers = false;
        [SerializeField] bool debugDraw = false;

        readonly Collider[] hitBuffer = new Collider[16];
        readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();

        void Awake()
        {
            if (attackOrigin == null)
            {
                attackOrigin = transform;
            }

            if (owner == null)
            {
                owner = GetComponentInParent<CharacterCombatAgent>();
            }

            if (owner != null && ownerTransform == null)
            {
                ownerTransform = owner.transform;
            }
        }

        public void SetOwner(CharacterCombatAgent combatAgent)
        {
            owner = combatAgent;
            if (owner != null && ownerTransform == null)
            {
                ownerTransform = owner.transform;
            }
        }

        /// <summary>
        /// Invoke this from an animation event when the weapon should apply damage.
        /// </summary>
        public void PerformAttack()
        {
            if (owner == null || !owner.IsAlive)
            {
                return;
            }

            hitThisSwing.Clear();

            int hits = Physics.OverlapSphereNonAlloc(
                attackOrigin.position,
                attackRadius,
                hitBuffer,
                hitMask,
                includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore);

            IDamageable bestTarget = null;
            Collider bestCollider = null;
            float bestDistanceSqr = float.PositiveInfinity;

            for (int i = 0; i < hits; i++)
            {
                Collider collider = hitBuffer[i];
                if (collider == null)
                {
                    continue;
                }

                if (ownerTransform != null && collider.transform.IsChildOf(ownerTransform))
                {
                    continue;
                }

                IDamageable damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive)
                {
                    continue;
                }

                if (hitThisSwing.Contains(damageable))
                {
                    continue;
                }

                Vector3 samplePoint = collider.ClosestPoint(attackOrigin.position);
                Vector3 toTarget = samplePoint - attackOrigin.position;
                if (toTarget.sqrMagnitude < Mathf.Epsilon)
                {
                    continue;
                }

                Vector3 forward = ownerTransform != null ? ownerTransform.forward : transform.forward;
                float angle = Vector3.Angle(forward, toTarget);
                if (angle > attackAngle * 0.5f)
                {
                    continue;
                }

                float sqrDistance = toTarget.sqrMagnitude;
                if (sqrDistance < bestDistanceSqr)
                {
                    bestDistanceSqr = sqrDistance;
                    bestTarget = damageable;
                    bestCollider = collider;
                }
            }

            if (bestTarget != null && bestCollider != null)
            {
                Vector3 hitPoint = bestCollider.ClosestPoint(attackOrigin.position);
                Vector3 hitNormal = (hitPoint - attackOrigin.position).sqrMagnitude > Mathf.Epsilon
                    ? (hitPoint - attackOrigin.position).normalized
                    : (ownerTransform != null ? ownerTransform.forward : transform.forward);
                owner.ResolveAttackHit(bestTarget, hitPoint, hitNormal, damageMultiplier);
                hitThisSwing.Add(bestTarget);
            }
            else
            {
                owner.NotifyAttackAnimationComplete();
            }
        }

        void OnDrawGizmosSelected()
        {
            if (attackOrigin == null)
            {
                attackOrigin = transform;
            }

            Gizmos.color = debugDraw ? Color.cyan : new Color(0f, 0.75f, 1f, 0.4f);
            Gizmos.DrawWireSphere(attackOrigin.position, attackRadius);

            if (ownerTransform != null)
            {
                Vector3 origin = attackOrigin.position;
                Vector3 forward = ownerTransform.forward;
                Quaternion leftRotation = Quaternion.AngleAxis(-attackAngle * 0.5f, Vector3.up);
                Quaternion rightRotation = Quaternion.AngleAxis(attackAngle * 0.5f, Vector3.up);
                Gizmos.DrawRay(origin, leftRotation * forward * attackRadius);
                Gizmos.DrawRay(origin, rightRotation * forward * attackRadius);
            }
        }
    }
}
