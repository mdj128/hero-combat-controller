using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeroCharacter
{
    /// <summary>
    /// Polls the animator for specific states and triggers the linked MeleeWeapon when the normalized time enters configured windows.
    /// Eliminates the need for per-clip animation events so both hero and NPCs can share the same attack clips.
    /// </summary>
    public class AnimatorAttackWindowDriver : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] MeleeWeapon weapon;
        [SerializeField] CharacterCombatAgent combatAgent;
        [SerializeField] List<AttackWindow> windows = new List<AttackWindow>
        {
            new AttackWindow
            {
                stateName = "Attack",
                layerIndex = 1,
                startNormalizedTime = 0.25f,
                endNormalizedTime = 0.4f
            }
        };

        void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (weapon == null)
            {
                weapon = GetComponentInChildren<MeleeWeapon>();
            }

            if (combatAgent == null)
            {
                combatAgent = GetComponent<CharacterCombatAgent>();
            }
        }

        void Update()
        {
            if (animator == null || weapon == null)
            {
                return;
            }

            if (!animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            {
                return;
            }

            for (int i = 0; i < windows.Count; i++)
            {
                var window = windows[i];
                if (window == null || string.IsNullOrEmpty(window.stateName))
                {
                    continue;
                }

                if (!animator.HasLayer(window.layerIndex))
                {
                    window.Reset();
                    continue;
                }

                var stateInfo = animator.GetCurrentAnimatorStateInfo(window.layerIndex);
                if (!stateInfo.IsName(window.stateName))
                {
                    if (window.triggered)
                    {
                        window.Reset();
                    }
                    continue;
                }

                float normalized = stateInfo.normalizedTime;
                if (!stateInfo.loop)
                {
                    normalized = Mathf.Clamp01(normalized);
                }
                else
                {
                    normalized = normalized - Mathf.Floor(normalized);
                }

                if (!window.triggered && normalized >= window.startNormalizedTime && normalized <= window.endNormalizedTime)
                {
                    weapon.PerformAttack();
                    window.triggered = true;
                }

                if (normalized > window.endNormalizedTime + window.resetBuffer || stateInfo.normalizedTime >= 1f - Mathf.Epsilon)
                {
                    window.Reset();
                }
            }
        }

        [Serializable]
        public class AttackWindow
        {
            public string stateName = "Attack";
            public int layerIndex = 1;
            [Range(0f, 1f)] public float startNormalizedTime = 0.3f;
            [Range(0f, 1f)] public float endNormalizedTime = 0.4f;
            [Tooltip("Small buffer after the window before the driver resets, to avoid multiple hits within the same animation.")]
            public float resetBuffer = 0.05f;

            [NonSerialized] public bool triggered;

            public void Reset()
            {
                triggered = false;
            }
        }
    }

    static class AnimatorExtensions
    {
        public static bool HasLayer(this Animator animator, int layerIndex)
        {
            return animator != null && layerIndex >= 0 && layerIndex < animator.layerCount;
        }
    }
}
