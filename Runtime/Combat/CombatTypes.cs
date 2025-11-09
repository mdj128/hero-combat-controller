using System;
using UnityEngine;
using UnityEngine.Events;

namespace HeroCharacter
{
    public enum DamageType
    {
        Physical,
        Fire,
        Ice,
        Lightning,
        Poison,
        True
    }

    [Serializable]
    public struct DamageInfo
    {
        public float amount;
        public DamageType damageType;
        public GameObject source;
        public GameObject instigator;
        public Vector3 point;
        public Vector3 normal;
        public bool unblockable;

        public DamageInfo(
            float amount,
            DamageType damageType,
            GameObject source,
            GameObject instigator,
            Vector3 point,
            Vector3 normal,
            bool unblockable = false)
        {
            this.amount = Mathf.Max(0f, amount);
            this.damageType = damageType;
            this.source = source;
            this.instigator = instigator;
            this.point = point;
            this.normal = normal;
            this.unblockable = unblockable;
        }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        void ApplyDamage(DamageInfo damage);
    }

    public interface ICombatHUD
    {
        void HandleHealthChanged(float current, float max);
        void HandleDamageTaken(DamageInfo damage, float current, float max);
        void HandleBlockState(bool isBlocking);
        void HandleDeath();
        void HandleRevive(float current, float max);
    }

    [Serializable]
    public class DamageInfoUnityEvent : UnityEvent<DamageInfo> { }

    [Serializable]
    public class HealthChangedUnityEvent : UnityEvent<float, float> { }

    [Serializable]
    public class BoolUnityEvent : UnityEvent<bool> { }
}
