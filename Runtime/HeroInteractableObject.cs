using System;
using UnityEngine;
using UnityEngine.Events;

namespace HeroCharacter
{
    /// <summary>
    /// Drop-in interactable that fires UnityEvents when the hero interacts.
    /// </summary>
    [DisallowMultipleComponent]
    public class HeroInteractableObject : MonoBehaviour, IHeroInteractable
    {
        [SerializeField] bool oneShot = false;
        [SerializeField] HeroInteractEvent onInteracted = new HeroInteractEvent();

        bool consumed;

        public void Interact(HeroCharacterController interactor)
        {
            if (consumed)
            {
                return;
            }

            onInteracted?.Invoke(interactor);

            if (oneShot)
            {
                consumed = true;
            }
        }
    }

    [Serializable]
    public class HeroInteractEvent : UnityEvent<HeroCharacterController> { }
}
