using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#else
#error HeroCharacterController requires Unity's Input System. Enable Active Input Handling (Input System Package) in Project Settings.
#endif

namespace HeroCharacter
{
    /// <summary>
    /// Custom character controller for combat and stats systems.
    /// Handles third-person camera control, grounded locomotion, sprinting and footstep audio.
    /// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(CharacterCombatAgent))]
    public class HeroCharacterController : MonoBehaviour, IDamageable
    {
        [SerializeField] CameraSettings cameraSettings = new CameraSettings();
        [SerializeField] MovementSettings movement = new MovementSettings();
        [SerializeField] FootstepSettings footsteps = new FootstepSettings();
        [SerializeField] InteractionSettings interaction = new InteractionSettings();
        [SerializeField] CrosshairSettings crosshair = new CrosshairSettings();
        [SerializeField] AnimationSettings animationSettings = new AnimationSettings();
        [SerializeField] InputSettings input = new InputSettings();
        [SerializeField] RuntimeEvents events = new RuntimeEvents();
        [SerializeField] DebugSettings debug = new DebugSettings();

        [Header("Feedback")]
        [SerializeField] bool attachFloatingCombatText = true;
        [SerializeField] Vector3 floatingCombatTextOffset = new Vector3(0f, 2f, 0f);
        [SerializeField] Color floatingCombatTextColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        [SerializeField] FloatingCombatTextSpawner floatingCombatText;

        Rigidbody body;
        CapsuleCollider capsule;
        AudioSource footstepAudio;
        CharacterCombatAgent combatAgent;
        HeroCrosshair crosshairUI;

        Vector2 moveInput;
        Vector2 lookInput;
        float zoomInput;
        bool sprintHeld;
        bool blockHeld;
        bool jumpRequested;
        bool attackRequested;
        bool attackAnimationRequested;
        bool interactRequested;
        bool suppressInputFrame;
        bool inputInitialized;
        bool lastBlockState;
        bool inputBindingsValid;
        bool playerInputMissingLogged;
        bool actionAssetMissingLogged;
        bool requiredActionsMissingLogged;

        float yaw;
        float pitch;
        float cameraDistance;
        float lookRampTimer;
        float stepCycle;
        float nextStepTime;

        bool isGrounded;
        bool wasGrounded;
        bool isSprinting;
        bool groundedSnap;
        bool sprintJumpActive;
        GroundState ground = new GroundState();
        Vector3 pendingRootMotion;
        Vector3 desiredPlanarVelocity;

        const float kSmallNumber = 0.0001f;

#if ENABLE_INPUT_SYSTEM
        InputAction moveAction;
        InputAction lookAction;
        InputAction zoomAction;
        InputAction jumpAction;
        InputAction sprintAction;
        InputAction attackAction;
        InputAction blockAction;
        InputAction interactAction;
#endif

        readonly Dictionary<Animator, Dictionary<string, AnimatorControllerParameterType>> animatorParameterCache = new Dictionary<Animator, Dictionary<string, AnimatorControllerParameterType>>();
        readonly HashSet<string> loggedMissingParameters = new HashSet<string>();

        #region Public API

        public event Action Jumped
        {
            add => events.Jumped += value;
            remove => events.Jumped -= value;
        }

        public event Action<GroundState> Grounded
        {
            add => events.Grounded += value;
            remove => events.Grounded -= value;
        }

        public event Action Airborne
        {
            add => events.Airborne += value;
            remove => events.Airborne -= value;
        }

        public event Action<IHeroInteractable> Interacted
        {
            add => events.Interact += value;
            remove => events.Interact -= value;
        }

        public event Action AttackRequested
        {
            add => events.AttackRequested += value;
            remove => events.AttackRequested -= value;
        }

        public event Action<bool> BlockStateChanged
        {
            add => events.BlockActive += value;
            remove => events.BlockActive -= value;
        }

        public event Action<float, float> HealthChanged
        {
            add => events.HealthChanged += value;
            remove => events.HealthChanged -= value;
        }

        public event Action<DamageInfo> DamageTaken
        {
            add => events.DamageTaken += value;
            remove => events.DamageTaken -= value;
        }

        public event Action Died
        {
            add => events.Died += value;
            remove => events.Died -= value;
        }

        public event Action Revived
        {
            add => events.Revived += value;
            remove => events.Revived -= value;
        }

        public event Action AttackPerformed
        {
            add => events.AttackPerformed += value;
            remove => events.AttackPerformed -= value;
        }

        public bool IsAlive => combatAgent != null && combatAgent.IsAlive;
        public CharacterCombatAgent Combat => combatAgent;

        public UnityEvent OnJumpEvent => events.onJump;
        public GroundStateEvent OnGroundedEvent => events.onGrounded;
        public UnityEvent OnAirborneEvent => events.onAirborne;
        public InteractEvent OnInteractEvent => events.onInteract;
        public UnityEvent OnAttackRequestedEvent => events.onAttackRequested;
        public BoolUnityEvent OnBlockActiveEvent => events.onBlockActive;
        public UnityEvent OnDeathEvent => events.onDied;
        public UnityEvent OnRevivedEvent => events.onRevived;
        public UnityEvent OnAttackPerformedEvent => events.onAttackPerformed;
        public HealthChangedUnityEvent OnHealthChangedEvent => events.onHealthChanged;
        public DamageInfoUnityEvent OnDamageTakenEvent => events.onDamageTaken;

        public void ApplyDamage(DamageInfo damage)
        {
            combatAgent?.ApplyDamage(damage);
        }

        #endregion

        #region Unity lifecycle

        void Reset()
        {
            cameraSettings.playerCamera = GetComponentInChildren<Camera>();
            footsteps.audioSource = GetComponent<AudioSource>();
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            combatAgent = GetComponent<CharacterCombatAgent>();
            EnsureFloatingCombatText();
            EnsureCrosshair();

            body.useGravity = true;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            cameraDistance = cameraSettings.thirdPersonDistance;
            lookRampTimer = 0f;
            nextStepTime = footsteps.stepInterval;

            CacheAudioSource();
            InitialiseCameraState();

            if (combatAgent == null)
            {
                Debug.LogError("HeroCharacterController requires a CharacterCombatAgent component.", this);
            }
        }

        void SubscribeCombatAgent()
        {
            if (combatAgent == null)
            {
                return;
            }

            combatAgent.HealthChanged += HandleCombatHealthChanged;
            combatAgent.DamageTaken += HandleCombatDamageTaken;
            combatAgent.Died += HandleCombatDied;
            combatAgent.Revived += HandleCombatRevived;
            combatAgent.AttackStarted += HandleCombatAttackStarted;
            combatAgent.AttackPerformed += HandleCombatAttackPerformed;
            combatAgent.BlockStateChanged += HandleCombatBlockStateChanged;
        }

        void UnsubscribeCombatAgent()
        {
            if (combatAgent == null)
            {
                return;
            }

            combatAgent.HealthChanged -= HandleCombatHealthChanged;
            combatAgent.DamageTaken -= HandleCombatDamageTaken;
            combatAgent.Died -= HandleCombatDied;
            combatAgent.Revived -= HandleCombatRevived;
            combatAgent.AttackStarted -= HandleCombatAttackStarted;
            combatAgent.AttackPerformed -= HandleCombatAttackPerformed;
            combatAgent.BlockStateChanged -= HandleCombatBlockStateChanged;
        }

        void EnsureFloatingCombatText()
        {
            if (!attachFloatingCombatText)
            {
                if (floatingCombatText != null)
                {
                    floatingCombatText.enabled = false;
                }
                return;
            }

            if (floatingCombatText == null)
            {
                floatingCombatText = GetComponent<FloatingCombatTextSpawner>();
            }

            if (floatingCombatText == null)
            {
                if (Application.isPlaying)
                {
                    floatingCombatText = gameObject.AddComponent<FloatingCombatTextSpawner>();
                }
                else
                {
                    return;
                }
            }

            ConfigureFloatingCombatTextSpawner();
        }

        void OnEnable()
        {
            if (combatAgent == null)
            {
                combatAgent = GetComponent<CharacterCombatAgent>();
            }

            SubscribeCombatAgent();
            EnableInputActions(true);
            suppressInputFrame = true;
            inputInitialized = false;
            lookRampTimer = 0f;
            if (cameraSettings.lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ConfigureFloatingCombatTextSpawner();
                EnsureCrosshair();
            }

            if (string.IsNullOrEmpty(animationSettings.forwardVelocityFloat))
            {
                animationSettings.forwardVelocityFloat = "MoveY";
            }
            if (string.IsNullOrEmpty(animationSettings.strafeVelocityFloat))
            {
                animationSettings.strafeVelocityFloat = "MoveX";
            }
        }
#endif

        void ConfigureFloatingCombatTextSpawner()
        {
            if (floatingCombatText == null)
            {
                return;
            }

            floatingCombatText.enabled = attachFloatingCombatText;
            floatingCombatText.Anchor = transform;
            floatingCombatText.SpawnOffset = floatingCombatTextOffset;
            floatingCombatText.DamageColor = floatingCombatTextColor;
        }

        void EnsureCrosshair()
        {
            if (cameraSettings.playerCamera == null)
            {
                return;
            }

            if (crosshairUI == null || crosshairUI.gameObject != cameraSettings.playerCamera.gameObject)
            {
                crosshairUI = cameraSettings.playerCamera.GetComponent<HeroCrosshair>();
            }

            if (crosshairUI == null && crosshair.enableCrosshair)
            {
                crosshairUI = cameraSettings.playerCamera.gameObject.AddComponent<HeroCrosshair>();
            }

            if (crosshairUI != null)
            {
            crosshairUI.ApplySettings(crosshair.enableCrosshair, crosshair.color, crosshair.interactableColor, crosshair.size, crosshair.thickness, crosshair.gap, crosshair.viewportAnchor);
        }
        }

        void OnDisable()
        {
            EnableInputActions(false);
            UnsubscribeCombatAgent();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            animatorParameterCache.Clear();
            loggedMissingParameters.Clear();
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureCrosshair();
            SampleInput();
            UpdateStateMachine();
            UpdateCamera(Time.deltaTime);
            UpdateAnimator(Time.deltaTime);
            UpdateFootsteps(Time.deltaTime);
            HandleInteractions();
            ClearTransientInputs();
        }

        void FixedUpdate()
        {
            if (!Application.isPlaying || !movement.enableMovementControl || cameraSettings.playerCamera == null)
            {
                return;
            }

            UpdateGrounding();
            ResolveLocomotion(Time.fixedDeltaTime);
            ApplyRootMotion();
        }

        #endregion

        #region Input

        void EnableInputActions(bool enable)
        {
            if (enable)
            {
                if (BindInputActions())
                {
                    SetActionsEnabled(true);
                }
            }
            else
            {
                SetActionsEnabled(false);
                ClearResolvedActions();
                inputBindingsValid = false;
            }
        }

        void SampleInput()
        {
            zoomInput = 0f;
            if (!inputInitialized)
            {
                suppressInputFrame = true;
            }

            if (!inputBindingsValid)
            {
                moveInput = Vector2.zero;
                lookInput = Vector2.zero;
                return;
            }

            moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            lookInput = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;

            bool jumpPressedThisFrame = jumpAction != null && jumpAction.WasPressedThisFrame();
            sprintHeld = sprintAction != null && sprintAction.IsPressed();
            bool attackPressedThisFrame = attackAction != null && attackAction.WasPressedThisFrame();
            blockHeld = blockAction != null && blockAction.IsPressed();
            bool interactPressedThisFrame = interactAction != null && interactAction.WasPressedThisFrame();
            float zoomValue = ReadZoomValue();

            jumpRequested |= jumpPressedThisFrame;
            attackRequested |= attackPressedThisFrame;
            interactRequested |= interactPressedThisFrame;
            zoomInput = zoomValue;

            moveInput = Vector2.ClampMagnitude(moveInput, 1f);

            if (suppressInputFrame)
            {
                moveInput = Vector2.zero;
                lookInput = Vector2.zero;
                zoomInput = 0f;
                jumpRequested = false;
                attackRequested = false;
                attackAnimationRequested = false;
                interactRequested = false;
                suppressInputFrame = false;
            }

            inputInitialized = true;
        }

        void ClearTransientInputs()
        {
            attackRequested = false;
            attackAnimationRequested = false;
            interactRequested = false;
            zoomInput = 0f;
        }

#if ENABLE_INPUT_SYSTEM
        bool BindInputActions()
        {
            SetActionsEnabled(false);
            ClearResolvedActions();

            PlayerInput playerInputRef = input.playerInput;
            if (input.autoBindFromPlayerInput || playerInputRef == null)
            {
                playerInputRef = GetComponent<PlayerInput>();
            }

            if (playerInputRef == null)
            {
                if (!playerInputMissingLogged)
                {
                    Debug.LogError("HeroCharacterController requires a PlayerInput component on the same GameObject.", this);
                    playerInputMissingLogged = true;
                }
                inputBindingsValid = false;
                return false;
            }

            input.playerInput = playerInputRef;
            var actionAsset = playerInputRef.actions;
            if (actionAsset == null)
            {
                if (!actionAssetMissingLogged)
                {
                    Debug.LogError("PlayerInput has no actions asset assigned.", playerInputRef);
                    actionAssetMissingLogged = true;
                }
                inputBindingsValid = false;
                return false;
            }

            moveAction = FindAction(actionAsset, input.moveAction);
            lookAction = FindAction(actionAsset, input.lookAction);
            zoomAction = FindAction(actionAsset, input.zoomAction);
            jumpAction = FindAction(actionAsset, input.jumpAction);
            sprintAction = FindAction(actionAsset, input.sprintAction);
            attackAction = FindAction(actionAsset, input.attackAction);
            blockAction = FindAction(actionAsset, input.blockAction);
            interactAction = FindAction(actionAsset, input.interactAction);

            inputBindingsValid = moveAction != null && lookAction != null;

            if (!inputBindingsValid)
            {
                if (!requiredActionsMissingLogged)
                {
                    Debug.LogError($"HeroCharacterController expects actions '{input.moveAction}' and '{input.lookAction}' on PlayerInput '{playerInputRef.name}'.", this);
                    requiredActionsMissingLogged = true;
                }
            }
            else
            {
                requiredActionsMissingLogged = false;
            }

            playerInputMissingLogged = false;
            actionAssetMissingLogged = false;

            return inputBindingsValid;
        }

        InputAction FindAction(InputActionAsset asset, string actionName)
        {
            if (asset == null || string.IsNullOrEmpty(actionName))
            {
                return null;
            }

            return asset.FindAction(actionName, false);
        }

        float ReadZoomValue()
        {
            float value = 0f;

            if (zoomAction != null)
            {
                string controlType = zoomAction.expectedControlType;
                if (string.Equals(controlType, "Vector2", StringComparison.OrdinalIgnoreCase))
                {
                    value = zoomAction.ReadValue<Vector2>().y;
                }
                else if (string.Equals(controlType, "Vector3", StringComparison.OrdinalIgnoreCase))
                {
                    value = zoomAction.ReadValue<Vector3>().y;
                }
                else
                {
                    value = zoomAction.ReadValue<float>();
                }
            }
            else if (Mouse.current != null)
            {
                value = Mouse.current.scroll.ReadValue().y;
            }

            return value;
        }

        void SetActionsEnabled(bool enable)
        {
            if (enable)
            {
                moveAction?.Enable();
                lookAction?.Enable();
                zoomAction?.Enable();
                jumpAction?.Enable();
                sprintAction?.Enable();
                attackAction?.Enable();
                blockAction?.Enable();
                interactAction?.Enable();
            }
            else
            {
                moveAction?.Disable();
                lookAction?.Disable();
                zoomAction?.Disable();
                jumpAction?.Disable();
                sprintAction?.Disable();
                attackAction?.Disable();
                blockAction?.Disable();
                interactAction?.Disable();
            }
        }

        void ClearResolvedActions()
        {
            moveAction = null;
            lookAction = null;
            zoomAction = null;
            jumpAction = null;
            sprintAction = null;
            attackAction = null;
            blockAction = null;
            interactAction = null;
        }

#endif

        #endregion

        #region Combat

        void HandleCombatHealthChanged(float current, float max)
        {
            events.InvokeHealthChanged(current, max);
        }

        void HandleCombatDamageTaken(DamageInfo damage, float currentHealth)
        {
            var animator = animationSettings.GetAnimator();
            TrySetTrigger(animator, animationSettings.damageTrigger);
            events.InvokeDamageTaken(damage);
        }

        void HandleCombatDied()
        {
            EnableInputActions(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            var animator = animationSettings.GetAnimator();
            TrySetTrigger(animator, animationSettings.deathTrigger);
            events.InvokeDeath();
        }

        void HandleCombatRevived()
        {
            events.InvokeRevived();
            if (!isActiveAndEnabled)
            {
                return;
            }

            EnableInputActions(true);
            suppressInputFrame = true;
            inputInitialized = false;
            lookRampTimer = 0f;
            if (cameraSettings.lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void HandleCombatAttackStarted()
        {
            attackAnimationRequested = true;
            events.InvokeAttackRequest();
        }

        void HandleCombatAttackPerformed()
        {
            events.InvokeAttackPerformed();
        }

        void HandleCombatBlockStateChanged(bool active)
        {
            blockHeld = active;
            lastBlockState = active;
            events.InvokeBlock(active);
        }

        #endregion

        #region State

        void UpdateStateMachine()
        {
            if (attackRequested)
            {
                combatAgent?.TryStartAttack();
                attackRequested = false;
            }

            if (blockHeld != lastBlockState)
            {
                lastBlockState = blockHeld;
                combatAgent?.SetBlocking(blockHeld);
            }
        }

        void UpdateGrounding()
        {
            wasGrounded = isGrounded;
            ground.Reset();

            float worldRadius = GetCapsuleWorldRadius();
            Vector3 origin = GetCapsuleBottomSphereCenter() + Vector3.up * movement.groundProbePadding;
            float rayLength = (GetCapsuleWorldHeight() * 0.5f) + movement.groundingProbeDepth;
            RaycastHit hit;
            float castRadius = Mathf.Max(0.01f, worldRadius - movement.groundProbePadding);
            if (Physics.SphereCast(origin, castRadius, Vector3.down, out hit, rayLength, movement.whatIsGround, QueryTriggerInteraction.Ignore))
            {
                ground.isGrounded = true;
                ground.normal = hit.normal;
                ground.point = hit.point;
                ground.angle = Vector3.Angle(hit.normal, Vector3.up);
                ground.collider = hit.collider;
                ground.physicsMaterial = hit.collider.sharedMaterial;
                ground.material = ResolveRenderMaterial(hit.collider);
                ground.terrainLayer = ResolveTerrainLayer(hit.collider, hit.point);

                if (!wasGrounded)
                {
                    events.InvokeGrounded(ground);
                }
            }

            isGrounded = ground.isGrounded;

            if (!wasGrounded && isGrounded)
            {
                groundedSnap = true;
                sprintJumpActive = false;
                TriggerLandingAnimation();
            }

            if (wasGrounded && !isGrounded)
            {
                events.InvokeAirborne();
            }
        }

        void ResolveLocomotion(float deltaTime)
        {
            if (!movement.enableMovementControl)
            {
                desiredPlanarVelocity = Vector3.zero;
                return;
            }

            Vector3 forward = cameraSettings.playerCamera != null
                ? Vector3.ProjectOnPlane(cameraSettings.playerCamera.transform.forward, Vector3.up).normalized
                : transform.forward;
            if (forward.sqrMagnitude < kSmallNumber)
            {
                forward = transform.forward;
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 moveWorld = (forward * moveInput.y) + (right * moveInput.x);
            moveWorld = Vector3.ClampMagnitude(moveWorld, 1f);

            Quaternion? desiredRotation = null;
            if (movement.faceCameraForward && forward.sqrMagnitude > kSmallNumber)
            {
                desiredRotation = Quaternion.LookRotation(forward, Vector3.up);
            }
            else if (moveWorld.sqrMagnitude > kSmallNumber)
            {
                desiredRotation = Quaternion.LookRotation(moveWorld, Vector3.up);
            }

            if (desiredRotation.HasValue)
            {
                transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation.Value, movement.rotationSharpness * deltaTime);
            }

            UpdateSprintState();

            float targetSpeed = movement.walkingSpeed;
            if (isSprinting)
            {
                targetSpeed = movement.sprintingSpeed;
            }
            else if (!isGrounded && sprintJumpActive)
            {
                targetSpeed = movement.sprintingSpeed;
            }

            desiredPlanarVelocity = moveWorld * targetSpeed;
            Vector3 currentPlanarVelocity = Vector3.ProjectOnPlane(GetVelocity(), Vector3.up);

            float accelRate = moveWorld.sqrMagnitude > kSmallNumber ? movement.acceleration : movement.deceleration;
            if (!isGrounded)
            {
                accelRate *= movement.airControl;
            }

            Vector3 newPlanarVelocity = Vector3.MoveTowards(currentPlanarVelocity, desiredPlanarVelocity, accelRate * deltaTime);
            Vector3 velocity = GetVelocity();
            velocity = newPlanarVelocity + Vector3.up * velocity.y;
            SetVelocity(velocity);

            if (groundedSnap)
            {
                Vector3 vel = GetVelocity();
                vel.y = Mathf.Min(vel.y, 0f);
                SetVelocity(vel);
                groundedSnap = false;
            }

            if (jumpRequested && movement.canJump)
            {
                if (isGrounded)
                {
                    PerformJump();
                }
                jumpRequested = false;
            }
        }

        void UpdateSprintState()
        {
            bool movePressed = moveInput.sqrMagnitude > 0.2f;
            bool allowed = movement.canSprint && isGrounded;
            if (movement.toggleSprint)
            {
                if (sprintHeld && !isSprinting && allowed && movePressed)
                {
                    isSprinting = true;
                }
                else if (sprintHeld && isSprinting && (!allowed || !movePressed))
                {
                    isSprinting = false;
                }
            }
            else
            {
                isSprinting = sprintHeld && allowed && movePressed;
            }

            if (!allowed)
            {
                isSprinting = false;
            }
        }

        void ApplyRootMotion()
        {
            if (pendingRootMotion.sqrMagnitude < kSmallNumber)
            {
                return;
            }

            body.MovePosition(body.position + pendingRootMotion);
            pendingRootMotion = Vector3.zero;
        }

        void PerformJump()
        {
            sprintJumpActive = isSprinting;
            Vector3 velocity = GetVelocity();
            velocity.y = 0f;
            SetVelocity(velocity);
            body.AddForce(Vector3.up * Mathf.Sqrt(2f * Physics.gravity.magnitude * movement.jumpHeight), ForceMode.VelocityChange);
            var anim = animationSettings.GetAnimator();
            if (TrySetTrigger(anim, animationSettings.jumpStartTrigger))
            {
                // trigger set
            }
            events.InvokeJump();
        }

        void TriggerLandingAnimation()
        {
            var anim = animationSettings.GetAnimator();
            TrySetTrigger(anim, animationSettings.jumpLandTrigger);
        }

        #endregion

        #region Camera

        void InitialiseCameraState()
        {
            if (cameraSettings.playerCamera == null)
            {
                return;
            }

            Vector3 targetPos = GetTargetCameraPosition();
            cameraSettings.playerCamera.transform.position = targetPos;
            yaw = transform.eulerAngles.y;
            pitch = cameraSettings.playerCamera.transform.eulerAngles.x;
        }

        void UpdateCamera(float deltaTime)
        {
            if (!cameraSettings.enableCameraControl || cameraSettings.playerCamera == null)
            {
                return;
            }

            float sensitivity = cameraSettings.sensitivity;
            float ramp = GetLookRampMultiplier(deltaTime);
            sensitivity *= ramp;
            Vector2 adjustedLook = lookInput;

            switch (cameraSettings.inversion)
            {
                case MouseInversionMode.InvertX:
                    adjustedLook.x *= -1f;
                    break;
                case MouseInversionMode.InvertY:
                    adjustedLook.y *= -1f;
                    break;
                case MouseInversionMode.InvertBoth:
                    adjustedLook *= -1f;
                    break;
            }

            yaw += adjustedLook.x * sensitivity * deltaTime;
            pitch -= adjustedLook.y * sensitivity * deltaTime;
            pitch = Mathf.Clamp(pitch, -cameraSettings.verticalRotationRange * 0.5f, cameraSettings.verticalRotationRange * 0.5f);

            float targetDistance = Mathf.Clamp(cameraSettings.thirdPersonDistance - zoomInput * cameraSettings.zoomSensitivity, cameraSettings.minThirdPersonDistance, cameraSettings.maxCameraDistance);
            cameraSettings.thirdPersonDistance = targetDistance;
            cameraDistance = SmoothValue(cameraDistance, targetDistance, cameraSettings.zoomSmoothing, deltaTime);

            float targetFov = isSprinting ? cameraSettings.sprintFov : cameraSettings.baseFov;
            cameraSettings.playerCamera.fieldOfView = Mathf.Lerp(cameraSettings.playerCamera.fieldOfView, targetFov, deltaTime * cameraSettings.fovAdjustSpeed);

            ApplyThirdPersonCamera(deltaTime);

            zoomInput = 0f;
        }

        float GetLookRampMultiplier(float deltaTime)
        {
            if (!cameraSettings.easeInLook)
            {
                return 1f;
            }

            lookRampTimer += deltaTime;

            if (lookRampTimer < cameraSettings.lookStartDelay)
            {
                return 0f;
            }

            if (cameraSettings.lookRampDuration <= kSmallNumber)
            {
                return 1f;
            }

            float t = Mathf.Clamp01((lookRampTimer - cameraSettings.lookStartDelay) / cameraSettings.lookRampDuration);
            return Mathf.SmoothStep(0f, 1f, t);
        }

        void ApplyThirdPersonCamera(float deltaTime)
        {
            float pivotHeight = movement.standingHeight + cameraSettings.thirdPersonPivotOffset;
            Vector3 pivot = transform.position + Vector3.up * pivotHeight;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 desiredOffset = rotation * new Vector3(0f, cameraSettings.thirdPersonVerticalOffset, -cameraDistance);
            Vector3 desiredPosition = pivot + desiredOffset;

            Vector3 backwards = rotation * Vector3.back;
            if (Physics.SphereCast(pivot, cameraSettings.obstructionRadius, backwards, out RaycastHit hit, cameraDistance, cameraSettings.obstructionMask, QueryTriggerInteraction.Ignore))
            {
                desiredPosition = hit.point + rotation * Vector3.forward * cameraSettings.collisionBuffer;
            }

            cameraSettings.playerCamera.transform.position = SmoothVector(cameraSettings.playerCamera.transform.position, desiredPosition, cameraSettings.thirdPersonPositionSmoothing, deltaTime);
            cameraSettings.playerCamera.transform.rotation = rotation;

            Quaternion target = Quaternion.Euler(0f, yaw, 0f);
            transform.rotation = Quaternion.Lerp(transform.rotation, target, cameraSettings.bodyAlignmentSmoothing * deltaTime);
        }

        Vector3 GetTargetCameraPosition()
        {
            float pivotHeight = movement.standingHeight + cameraSettings.thirdPersonPivotOffset;
            Vector3 pivot = transform.position + Vector3.up * pivotHeight;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            return pivot + rotation * new Vector3(0f, cameraSettings.thirdPersonVerticalOffset, -cameraSettings.thirdPersonDistance);
        }

        #endregion

        #region Footsteps

        void UpdateFootsteps(float deltaTime)
        {
            if (!footsteps.enableFootsteps || footstepAudio == null)
            {
                return;
            }

            if (!isGrounded || desiredPlanarVelocity.sqrMagnitude < kSmallNumber)
            {
                stepCycle = 0f;
                return;
            }

            stepCycle += (desiredPlanarVelocity.magnitude + (isSprinting ? footsteps.sprintStepMultiplier : 1f)) * deltaTime;
            if (stepCycle > nextStepTime)
            {
                nextStepTime = stepCycle + footsteps.stepInterval;
                PlayFootstep();
            }
        }

        void PlayFootstep()
        {
            var profile = footsteps.GetProfileForGround(ground);
            if (profile == null || profile.footstepClips.Count == 0)
            {
                return;
            }

            AudioClip clip = profile.footstepClips[UnityEngine.Random.Range(0, profile.footstepClips.Count)];
            footstepAudio.pitch = UnityEngine.Random.Range(footsteps.pitchRange.x, footsteps.pitchRange.y);
            footstepAudio.volume = footsteps.volume;
            footstepAudio.PlayOneShot(clip);
        }

        void CacheAudioSource()
        {
            footstepAudio = footsteps.audioSource != null ? footsteps.audioSource : GetComponent<AudioSource>();
            if (footstepAudio == null && footsteps.enableFootsteps)
            {
                footstepAudio = gameObject.AddComponent<AudioSource>();
                footstepAudio.spatialBlend = 1f;
                footstepAudio.playOnAwake = false;
            }
        }

        #endregion

        #region Animator & Interaction

        void UpdateAnimator(float deltaTime)
        {
            if (animationSettings.thirdPersonAnimator != null)
            {
                animationSettings.thirdPersonAnimator.applyRootMotion = animationSettings.applyRootMotion;
            }

            var anim = animationSettings.GetAnimator();
            if (anim == null)
            {
                return;
            }

            // Fallback to common parameter names if left blank in the inspector.
            if (string.IsNullOrEmpty(animationSettings.forwardVelocityFloat))
            {
                animationSettings.forwardVelocityFloat = "MoveY";
            }
            if (string.IsNullOrEmpty(animationSettings.strafeVelocityFloat))
            {
                animationSettings.strafeVelocityFloat = "MoveX";
            }

            Vector3 planarVelocity = Vector3.ProjectOnPlane(GetVelocity(), Vector3.up);
            float velocity = planarVelocity.magnitude;
            if (velocity < animationSettings.velocityEpsilon)
            {
                velocity = 0f;
            }

            Vector3 localPlanarVelocity = transform.InverseTransformVector(planarVelocity);
            // Use local velocity so run/walk blend trees see real movement speed (covers sprint magnitudes).
            float forwardParam = Mathf.Clamp(localPlanarVelocity.z, -10f, 10f);
            float strafeParam = Mathf.Clamp(localPlanarVelocity.x, -10f, 10f);

            TrySetFloat(anim, animationSettings.velocityFloat, velocity, deltaTime);
            TrySetFloatImmediate(anim, animationSettings.forwardVelocityFloat, forwardParam);
            TrySetFloatImmediate(anim, animationSettings.strafeVelocityFloat, strafeParam);
            TrySetBool(anim, animationSettings.groundedBool, isGrounded);
            TrySetBool(anim, animationSettings.sprintBool, isSprinting);
            TrySetBool(anim, animationSettings.idleBool, velocity <= 0f);
            if (attackAnimationRequested && !string.IsNullOrEmpty(animationSettings.attackTrigger))
            {
                TrySetTrigger(anim, animationSettings.attackTrigger);
            }
            attackAnimationRequested = false;
        }

        bool TrySetFloat(Animator animator, string parameter, float value, float deltaTime)
        {
            if (string.IsNullOrEmpty(parameter) || !AnimatorHasParameter(animator, parameter, AnimatorControllerParameterType.Float))
            {
                return false;
            }

            animator.SetFloat(parameter, value, animationSettings.dampTime, deltaTime);
            return true;
        }

        bool TrySetFloatImmediate(Animator animator, string parameter, float value)
        {
            if (string.IsNullOrEmpty(parameter) || !AnimatorHasParameter(animator, parameter, AnimatorControllerParameterType.Float))
            {
                return false;
            }

            animator.SetFloat(parameter, value);
            return true;
        }

        bool TrySetBool(Animator animator, string parameter, bool value)
        {
            if (string.IsNullOrEmpty(parameter) || !AnimatorHasParameter(animator, parameter, AnimatorControllerParameterType.Bool))
            {
                return false;
            }

            animator.SetBool(parameter, value);
            return true;
        }

        bool TrySetTrigger(Animator animator, string parameter)
        {
            if (string.IsNullOrEmpty(parameter) || !AnimatorHasParameter(animator, parameter, AnimatorControllerParameterType.Trigger))
            {
                return false;
            }

            animator.SetTrigger(parameter);
            return true;
        }

        bool AnimatorHasParameter(Animator animator, string parameter, AnimatorControllerParameterType expectedType)
        {
            if (animator == null)
            {
                return false;
            }

            if (!animatorParameterCache.TryGetValue(animator, out var cache) || cache == null)
            {
                cache = BuildAnimatorParameterCache(animator);
            }

            if (cache.TryGetValue(parameter, out var cachedType))
            {
                if (cachedType == expectedType)
                {
                    return true;
                }

                LogParameterTypeMismatch(animator, parameter, expectedType, cachedType);
                return false;
            }

            LogMissingParameter(animator, parameter, expectedType);
            return false;
        }

        Dictionary<string, AnimatorControllerParameterType> BuildAnimatorParameterCache(Animator animator)
        {
            var cache = new Dictionary<string, AnimatorControllerParameterType>();
            foreach (var param in animator.parameters)
            {
                cache[param.name] = param.type;
            }
            animatorParameterCache[animator] = cache;
            return cache;
        }

        void LogMissingParameter(Animator animator, string parameter, AnimatorControllerParameterType expectedType)
        {
            string controllerName = animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : animator.name;
            string key = controllerName + ":" + parameter + ":missing:" + expectedType;
            if (loggedMissingParameters.Contains(key))
            {
                return;
            }

            loggedMissingParameters.Add(key);
            Debug.LogWarning($"Animator '{controllerName}' is missing {expectedType} parameter '{parameter}'.", animator);
        }

        void LogParameterTypeMismatch(Animator animator, string parameter, AnimatorControllerParameterType expectedType, AnimatorControllerParameterType actualType)
        {
            string controllerName = animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : animator.name;
            string key = controllerName + ":" + parameter + ":mismatch:" + expectedType + ":" + actualType;
            if (loggedMissingParameters.Contains(key))
            {
                return;
            }

            loggedMissingParameters.Add(key);
            Debug.LogWarning($"Animator '{controllerName}' parameter '{parameter}' is {actualType}, but the controller expected {expectedType}.", animator);
        }

        void OnAnimatorMove()
        {
            if (!animationSettings.applyRootMotion)
            {
                return;
            }

            var anim = animationSettings.GetAnimator();
            if (anim == null)
            {
                return;
            }

            pendingRootMotion += anim.deltaPosition;
            transform.rotation *= anim.deltaRotation;
        }

        void HandleInteractions()
        {
            bool hasTarget;
            IHeroInteractable target = FindInteractable(out hasTarget);
            UpdateCrosshairHover(hasTarget);

            if (!interactRequested || interaction.interactableLayer == 0)
            {
                interactRequested = false;
                return;
            }

            if (target != null)
            {
                target.Interact(this);
                events.InvokeInteract(target);
            }
            interactRequested = false;
        }

        void UpdateCrosshairHover(bool hasTarget)
        {
            if (crosshairUI != null)
            {
                crosshairUI.SetInteractableHover(hasTarget);
            }
        }

        IHeroInteractable FindInteractable(out bool hasTarget)
        {
            hasTarget = false;
            if (cameraSettings.playerCamera == null)
            {
                return null;
            }

            if (interaction.interactableLayer == 0)
            {
                return null;
            }

            Camera cam = cameraSettings.playerCamera;
            Vector2 anchor = crosshair.viewportAnchor;
            Ray ray = cam.ViewportPointToRay(new Vector3(anchor.x, anchor.y, 0f));

            if (Physics.SphereCast(ray, interaction.sphereCastRadius, out var hit, interaction.interactRange, interaction.interactableLayer, QueryTriggerInteraction.Collide))
            {
                if (TryResolveInteractable(hit.collider, out var interactable))
                {
                    hasTarget = true;
                    return interactable;
                }
            }

            // Fallback: proximity search in front of the hero to keep legacy behaviour.
            Collider[] colliders = Physics.OverlapBox(transform.position + transform.forward * (interaction.interactRange * 0.5f),
                Vector3.one * (interaction.interactRange * 0.5f), transform.rotation, interaction.interactableLayer, QueryTriggerInteraction.Ignore);

            float closestDistance = float.MaxValue;
            IHeroInteractable closest = null;
            foreach (var collider in colliders)
            {
                if (!TryResolveInteractable(collider, out var interactable))
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, collider.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = interactable;
                }
            }

            hasTarget = closest != null;
            return closest;
        }

        bool TryResolveInteractable(Collider collider, out IHeroInteractable interactable)
        {
            if (collider == null)
            {
                interactable = null;
                return false;
            }

            if (collider.TryGetComponent<IHeroInteractable>(out interactable))
            {
                return true;
            }

            interactable = collider.GetComponentInParent<IHeroInteractable>();
            return interactable != null;
        }

        #endregion

        #region Utility

        public bool IsGrounded => isGrounded;
        public bool IsSprinting => isSprinting;
        public GroundState GroundInfo => ground;

        Vector3 GetVelocity()
        {
#if UNITY_6000_0_OR_NEWER
            return body.linearVelocity;
#else
            return body.velocity;
#endif
        }

        void SetVelocity(Vector3 velocity)
        {
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = velocity;
#else
            body.velocity = velocity;
#endif
        }

        float SmoothValue(float current, float target, float smoothing, float deltaTime)
        {
            if (smoothing <= 0f)
            {
                return target;
            }

            float t = 1f - Mathf.Exp(-smoothing * deltaTime);
            return Mathf.Lerp(current, target, t);
        }

        Vector3 SmoothVector(Vector3 current, Vector3 target, float smoothing, float deltaTime)
        {
            if (smoothing <= 0f)
            {
                return target;
            }

            float t = 1f - Mathf.Exp(-smoothing * deltaTime);
            return Vector3.Lerp(current, target, t);
        }

        Material ResolveRenderMaterial(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            var renderer = collider.GetComponent<Renderer>();
            if (renderer == null)
            {
                return null;
            }

            var materials = renderer.sharedMaterials;
            if (materials != null && materials.Length > 0)
            {
                return materials[0];
            }

            return renderer.sharedMaterial;
        }

        TerrainLayer ResolveTerrainLayer(Collider collider, Vector3 hitPoint)
        {
            if (collider == null)
            {
                return null;
            }

            var terrain = collider.GetComponent<Terrain>();
            if (terrain == null)
            {
                return null;
            }

            var data = terrain.terrainData;
            if (data == null || data.terrainLayers == null || data.terrainLayers.Length == 0)
            {
                return null;
            }

            Vector3 terrainPos = hitPoint - terrain.transform.position;
            Vector3 normalized = new Vector3(
                Mathf.Clamp01(terrainPos.x / data.size.x),
                Mathf.Clamp01(terrainPos.y / data.size.y),
                Mathf.Clamp01(terrainPos.z / data.size.z));

            int mapX = Mathf.Clamp(Mathf.RoundToInt(normalized.x * (data.alphamapWidth - 1)), 0, data.alphamapWidth - 1);
            int mapZ = Mathf.Clamp(Mathf.RoundToInt(normalized.z * (data.alphamapHeight - 1)), 0, data.alphamapHeight - 1);
            float[,,] weights = data.GetAlphamaps(mapX, mapZ, 1, 1);

            int layerIndex = 0;
            float highestWeight = 0f;
            for (int i = 0; i < weights.GetLength(2); i++)
            {
                float w = weights[0, 0, i];
                if (w > highestWeight)
                {
                    highestWeight = w;
                    layerIndex = i;
                }
            }

            return data.terrainLayers[Mathf.Clamp(layerIndex, 0, data.terrainLayers.Length - 1)];
        }

        #endregion

        #region Editor Gizmos

        void OnDrawGizmosSelected()
        {
            if (!debug.drawGroundingGizmos)
            {
                return;
            }

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(GetCapsuleBottomSphereCenter(), GetCapsuleWorldRadius());
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(GetCapsuleBottomSphereCenter() - Vector3.up * movement.groundingProbeDepth, GetCapsuleWorldRadius());
            if (ground.isGrounded)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(ground.point, 0.05f);
                Gizmos.DrawLine(ground.point, ground.point + ground.normal);
            }
        }

        #endregion

        #region Nested types

        Vector3 GetCapsuleBottomSphereCenter()
        {
            if (capsule == null)
            {
                return transform.position;
            }

            Bounds bounds = capsule.bounds;
            float worldRadius = GetCapsuleWorldRadius();
            Vector3 center = bounds.center;
            center.y = bounds.min.y + worldRadius;
            return center;
        }

        Vector3 GetCapsuleBottom()
        {
            if (capsule == null)
            {
                return transform.position;
            }

            float worldRadius = GetCapsuleWorldRadius();
            Vector3 bottomCenter = GetCapsuleBottomSphereCenter();
            bottomCenter.y -= worldRadius;
            return bottomCenter;
        }

        float GetCapsuleWorldRadius()
        {
            if (capsule == null)
            {
                return 0.5f;
            }

            Vector3 lossy = capsule.transform.lossyScale;
            float axisScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z));
            return capsule.radius * axisScale;
        }

        float GetCapsuleWorldHeight()
        {
            if (capsule == null)
            {
                return 1.8f;
            }

            return Mathf.Abs(capsule.height * capsule.transform.lossyScale.y);
        }

        [Serializable]
        class CameraSettings
        {
            [Header("References")]
            public Camera playerCamera;

            [Header("Camera Control")]
            public bool enableCameraControl = true;
            public bool lockCursor = true;
            public MouseInversionMode inversion = MouseInversionMode.None;

            [Header("Look")]
            public float sensitivity = 50f;
            public float verticalRotationRange = 170f;
            public float bodyAlignmentSmoothing = 0f;
            [Tooltip("If true, mouse look eases in on scene start/enable to avoid sudden jumps.")]
            public bool easeInLook = true;
            [Tooltip("Delay (seconds) after enable before look begins to ramp.")]
            public float lookStartDelay = 0.15f;
            [Tooltip("Time (seconds) the look sensitivity takes to reach full strength after the delay.")]
            public float lookRampDuration = 0.35f;

            [Header("Third Person")]
            public float thirdPersonDistance = 3f;
            public float maxCameraDistance = 8f;
            public float minThirdPersonDistance = 1f;
            public float thirdPersonVerticalOffset = 0f;
            public float zoomSensitivity = 0.5f;
            public float zoomSmoothing = 0f;
            public float thirdPersonPositionSmoothing = 0f;
            [Tooltip("Offset applied to the pivot height used for third-person camera orbit.")]
            public float thirdPersonPivotOffset = -0.5f;
            public float obstructionRadius = 0.2f;
            public float collisionBuffer = 0.3f;
            public LayerMask obstructionMask = ~0;

            [Header("Field of View")]
            public float baseFov = 70f;
            public float sprintFov = 80f;
            public float fovAdjustSpeed = 5f;
        }

        [Serializable]
        class MovementSettings
        {
            public bool enableMovementControl = true;
            public bool canJump = true;
            public bool canSprint = true;
            public bool toggleSprint = false;
            [Tooltip("If true, the character faces the camera-forward direction, enabling strafing/backpedal without turning around.")]
            public bool faceCameraForward = true;
            public float standingHeight = 1.8f;
            public float walkingSpeed = 3.5f;
            public float sprintingSpeed = 6f;
            public float acceleration = 24f;
            public float deceleration = 18f;
            public float rotationSharpness = 12f;
            public float airControl = 0.4f;
            public float jumpHeight = 1.3f;
            public float groundingProbeDepth = 0.2f;
            public float groundProbePadding = 0.02f;
            public LayerMask whatIsGround = Physics.DefaultRaycastLayers;
        }

        [Serializable]
        class FootstepSettings
        {
            public bool enableFootsteps = true;
            public AudioSource audioSource;
            public float stepInterval = 0.4f;
            public float sprintStepMultiplier = 1.4f;
            public float volume = 0.8f;
            public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
            public List<GroundMaterialProfile> profiles = new List<GroundMaterialProfile>();

            public GroundMaterialProfile GetProfileForGround(GroundState ground)
            {
                if (!ground.isGrounded)
                {
                    return null;
                }

                foreach (var profile in profiles)
                {
                    if (profile == null)
                    {
                        continue;
                    }

                    if (profile.Matches(ground))
                    {
                        return profile;
                    }
                }

                return null;
            }
        }

        [Serializable]
        class InteractionSettings
        {
            public float interactRange = 3f;
            public float sphereCastRadius = 0.25f;
            public LayerMask interactableLayer = ~0;
        }

        [Serializable]
        class CrosshairSettings
        {
            public bool enableCrosshair = true;
            public Color color = new Color(1f, 1f, 1f, 0.7f);
            public Color interactableColor = new Color(0.4f, 1f, 0.4f, 0.9f);
            public float size = 14f;
            public float thickness = 2f;
            public float gap = 6f;
            [Tooltip("Viewport anchor for the crosshair (0-1). Slight offsets push the reticle off center for over-the-shoulder cameras.")]
            public Vector2 viewportAnchor = new Vector2(0.55f, 0.52f);
        }

        [Serializable]
        class AnimationSettings
        {
            public Animator thirdPersonAnimator;
            public string velocityFloat = "Vel";
            public string forwardVelocityFloat = "MoveY";
            public string strafeVelocityFloat = "MoveX";
            public string groundedBool = "Grounded";
            public string sprintBool = "Sprinting";
            public string idleBool = "Idle";
            [FormerlySerializedAs("jumpTrigger")] public string jumpStartTrigger = "Jump";
            public string jumpLandTrigger = "";
            public string attackTrigger = "Attacking";
            public string damageTrigger = "Damage";
            public string deathTrigger = "Death";
            public float dampTime = 0.1f;
            public float velocityEpsilon = 0.05f;
            public bool applyRootMotion = false;

            public Animator GetAnimator()
            {
                return thirdPersonAnimator;
            }
        }

        [Serializable]
        class InputSettings
        {
            public bool autoBindFromPlayerInput = true;
            public PlayerInput playerInput;
            public string moveAction = "Move";
            public string lookAction = "Look";
            public string zoomAction = "Zoom";
            public string jumpAction = "Jump";
            public string sprintAction = "Sprint";
            public string attackAction = "Attack";
            public string blockAction = "Block";
            public string interactAction = "Interact";
        }

        [Serializable]
        class RuntimeEvents
        {
            public UnityEvent onJump = new UnityEvent();
            public GroundStateEvent onGrounded = new GroundStateEvent();
            public UnityEvent onAirborne = new UnityEvent();
            public InteractEvent onInteract = new InteractEvent();
            public UnityEvent onAttackRequested = new UnityEvent();
            public UnityEvent onAttackPerformed = new UnityEvent();
            public BoolUnityEvent onBlockActive = new BoolUnityEvent();
            public HealthChangedUnityEvent onHealthChanged = new HealthChangedUnityEvent();
            public DamageInfoUnityEvent onDamageTaken = new DamageInfoUnityEvent();
            public UnityEvent onDied = new UnityEvent();
            public UnityEvent onRevived = new UnityEvent();

            [NonSerialized] public Action Jumped;
            [NonSerialized] public Action<GroundState> Grounded;
            [NonSerialized] public Action Airborne;
            [NonSerialized] public Action<IHeroInteractable> Interact;
            [NonSerialized] public Action AttackRequested;
            [NonSerialized] public Action AttackPerformed;
            [NonSerialized] public Action<bool> BlockActive;
            [NonSerialized] public Action<float, float> HealthChanged;
            [NonSerialized] public Action<DamageInfo> DamageTaken;
            [NonSerialized] public Action Died;
            [NonSerialized] public Action Revived;

            public void InvokeJump()
            {
                onJump.Invoke();
                Jumped?.Invoke();
            }

            public void InvokeGrounded(GroundState ground)
            {
                onGrounded.Invoke(ground);
                Grounded?.Invoke(ground);
            }

            public void InvokeAirborne()
            {
                onAirborne.Invoke();
                Airborne?.Invoke();
            }

            public void InvokeInteract(IHeroInteractable target)
            {
                GameObject go = null;
                if (target is Component component)
                {
                    go = component.gameObject;
                }
                onInteract.Invoke(go);
                Interact?.Invoke(target);
            }

            public void InvokeAttackRequest()
            {
                onAttackRequested.Invoke();
                AttackRequested?.Invoke();
            }

            public void InvokeAttackPerformed()
            {
                onAttackPerformed.Invoke();
                AttackPerformed?.Invoke();
            }

            public void InvokeBlock(bool active)
            {
                onBlockActive.Invoke(active);
                BlockActive?.Invoke(active);
            }

            public void InvokeHealthChanged(float current, float max)
            {
                onHealthChanged.Invoke(current, max);
                HealthChanged?.Invoke(current, max);
            }

            public void InvokeDamageTaken(DamageInfo damage)
            {
                onDamageTaken.Invoke(damage);
                DamageTaken?.Invoke(damage);
            }

            public void InvokeDeath()
            {
                onDied.Invoke();
                Died?.Invoke();
            }

            public void InvokeRevived()
            {
                onRevived.Invoke();
                Revived?.Invoke();
            }
        }

        [Serializable]
        public class BoolUnityEvent : UnityEvent<bool> { }

        [Serializable]
        public class GroundStateEvent : UnityEvent<GroundState> { }

        [Serializable]
        public class InteractEvent : UnityEvent<GameObject> { }

        [Serializable]
        class DebugSettings
        {
            public bool drawGroundingGizmos = false;
        }

        #endregion
    }

    public enum MouseInversionMode
    {
        None,
        InvertX,
        InvertY,
        InvertBoth
    }

    [Serializable]
    public class GroundMaterialProfile
    {
        public GroundMatchMode matchMode = GroundMatchMode.Material;
        public List<Material> materials = new List<Material>();
        public List<PhysicsMaterial> physicsMaterials = new List<PhysicsMaterial>();
        public List<TerrainLayer> terrainLayers = new List<TerrainLayer>();
        public LayerMask additionalLayerMask = 0;
        public List<AudioClip> footstepClips = new List<AudioClip>();

        public bool Matches(GroundState ground)
        {
            if (!ground.isGrounded)
            {
                return false;
            }

            switch (matchMode)
            {
                case GroundMatchMode.Material:
                    if (ground.material != null && materials.Contains(ground.material))
                    {
                        return true;
                    }
                    break;
                case GroundMatchMode.PhysicsMaterial:
                    if (ground.physicsMaterial != null && physicsMaterials.Contains(ground.physicsMaterial))
                    {
                        return true;
                    }
                    break;
                case GroundMatchMode.TerrainLayer:
                    if (ground.terrainLayer != null && terrainLayers.Contains(ground.terrainLayer))
                    {
                        return true;
                    }
                    break;
                case GroundMatchMode.LayerMask:
                    if (ground.collider != null && ((1 << ground.collider.gameObject.layer) & additionalLayerMask) != 0)
                    {
                        return true;
                    }
                    break;
            }

            return false;
        }
    }

    public enum GroundMatchMode
    {
        Material,
        PhysicsMaterial,
        TerrainLayer,
        LayerMask
    }

    [Serializable]
    public class GroundState
    {
        public bool isGrounded;
        public float angle;
        public Vector3 normal;
        public Vector3 point;
        public Material material;
        public PhysicsMaterial physicsMaterial;
        public TerrainLayer terrainLayer;
        public Collider collider;

        public void Reset()
        {
            isGrounded = false;
            angle = 0f;
            normal = Vector3.up;
            point = Vector3.zero;
            material = null;
            physicsMaterial = null;
            terrainLayer = null;
            collider = null;
        }
    }

    public interface IHeroInteractable
    {
        void Interact(HeroCharacterController interactor);
    }
}
