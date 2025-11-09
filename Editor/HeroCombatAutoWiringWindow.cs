#if ENABLE_INPUT_SYSTEM
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HeroCharacter.Editor
{
    public class HeroCombatAutoWiringWindow : EditorWindow
    {
        const string MenuPath = "Hero Character/Combat Auto Wiring";

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<HeroCombatAutoWiringWindow>("Hero Combat Auto Wiring");
            window.minSize = new Vector2(420f, 520f);
            window.Show();
        }

        GameObject heroInstance;
        GameObject npcInstance;
        Camera heroCameraOverride;
        Animator heroAnimatorOverride;
        AudioSource heroAudioOverride;
        Transform heroWeaponOverride;

        Transform npcWeaponOverride;

        InputActionAsset inputActions;
        bool heroAddWeapon = true;
        bool heroAutoCreateCamera = true;
        bool heroForceReplaceCamera;
        bool heroAutoCreateAudio = true;
        bool heroTryAutoWeapon = true;
        bool heroHideRigHierarchy = true;
        bool heroTagAsPlayer = true;
        bool npcTagAsEnemy = true;
        bool npcAddCollider = true;
        bool npcTryAutoWeapon = true;
        bool heroOverrideLayer;
        bool npcOverrideLayer;
        int heroLayer = 0;
        int npcLayer = 0;
        float heroCameraDistance = 3f;
        float heroCameraPivotOffset = -0.5f;
        float heroCameraVerticalOffset = 0f;
        float heroWalkSpeed = 3.5f;
        float heroSprintSpeed = 6f;
        float heroMaxHealth = 100f;
        float heroAttackDamage = 25f;
        float heroAttackCooldown = 0.6f;
        float heroBlockMultiplier = 0.4f;
        float heroPostHitInvuln = 0.15f;
        float heroDamageVariance = 0.3f;
        bool heroAttachFloatingText = true;
        Vector3 heroFloatingTextOffset = new Vector3(0f, 2f, 0f);
        Color heroFloatingTextColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        float heroMeleeRadius = 2f;
        float heroMeleeAngle = 120f;
        float heroMeleeDamageMultiplier = 1f;
        LayerMask heroGroundMask = ~0;
        LayerMask heroMeleeMask = ~0;
        bool heroMeleeIncludeTriggers;
        string heroTag = "Player";
        string npcTag = "Enemy";

        float npcMaxHealth = 100f;
        float npcAttackDamage = 10f;
        float npcAttackCooldown = 0.6f;
        float npcAcquireRange = 5f;
        float npcAttackRange = 1.9f;
        float npcTurnSpeed = 7f;
        float npcDamageScale = 1f;
        float npcLeashRange = 10f;
        float npcReturnSpeed = 3f;
        float npcAttackDelay = 1f;
        float npcDamageVariance = 0.3f;
        bool npcEnableMovement = true;
        bool npcAllowSprint = true;
        float npcWalkSpeed = 1.5f;
        float npcSprintSpeed = 3f;
        float npcSprintDistance = 3f;
        float npcStoppingBuffer = 0.25f;
        float npcColliderRadius = 0.2f;
        float npcColliderHeight = 1.8f;
        float npcMeleeRadius = 1.5f;
        float npcMeleeAngle = 120f;
        float npcMeleeDamageMultiplier = 1f;
        LayerMask npcMeleeMask = ~0;
        bool npcMeleeIncludeTriggers;
        string npcAttackStateName = "Attack";
        int npcAttackLayer = 1;
        float npcAttackWindowStart = 0.3f;
        float npcAttackWindowEnd = 0.45f;
        bool npcAttachHealthBar = true;
        Vector3 npcHealthBarOffset = new Vector3(0f, 1.6f, 0f);
        Vector2 npcHealthBarSize = new Vector2(1.2f, 0.18f);
        float npcHealthBarScale = 1f;
        bool npcHealthBarAlwaysVisible;
        bool npcAttachFloatingText = true;
        Vector3 npcFloatingTextOffset = new Vector3(0f, 2.1f, 0f);
        Color npcFloatingTextColor = new Color(0.235f, 0.776f, 0.851f, 1f);
        HeroCharacterController heroTargetOverride;

        AnimationBindingProfile animationProfile = AnimationBindingProfile.CreateDefault();
        Vector2 scroll;
        string lastReport;
        MessageType lastReportType = MessageType.Info;
        bool showAnimationFoldout = true;
        static bool? heroAnimatorHasRequiredClips;

        void OnEnable()
        {
            if (inputActions == null)
            {
                inputActions = HeroCombatAutoWireUtility.FindDefaultInputAsset();
            }

            heroAnimatorHasRequiredClips = null;
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Select your in-scene hero/NPC objects and press the configure buttons to auto-wire combat, weapons, and HUD bindings.", MessageType.Info);
            DrawAnimatorDependencyWarning();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawHeroSection();
            EditorGUILayout.Space(12f);
            DrawNpcSection();
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(lastReport))
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Last Action", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(lastReport, lastReportType);
            }
        }

        void DrawAnimatorDependencyWarning()
        {
            if (!heroAnimatorHasRequiredClips.HasValue)
            {
                heroAnimatorHasRequiredClips = HeroCombatAutoWireUtility.HeroAnimatorHasAllRequiredClips();
            }

            if (heroAnimatorHasRequiredClips.Value)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "HeroCombatAnimator.controller references the Kevin Iglesias Free Combat Animations pack. Install those animations or replace the controller's clips; otherwise you must assign your own animations for every state.",
                MessageType.Warning);
        }

        void DrawHeroSection()
        {
            EditorGUILayout.LabelField("Hero Setup", EditorStyles.boldLabel);
            heroInstance = (GameObject)EditorGUILayout.ObjectField("Hero In Scene", heroInstance, typeof(GameObject), true);
            heroCameraOverride = (Camera)EditorGUILayout.ObjectField("Camera Override", heroCameraOverride, typeof(Camera), true);
            heroAnimatorOverride = (Animator)EditorGUILayout.ObjectField("Animator Override", heroAnimatorOverride, typeof(Animator), true);
            heroAudioOverride = (AudioSource)EditorGUILayout.ObjectField("Audio Override", heroAudioOverride, typeof(AudioSource), true);
            heroWeaponOverride = (Transform)EditorGUILayout.ObjectField("Weapon / Attack Origin", heroWeaponOverride, typeof(Transform), true);

            EditorGUILayout.Space(4f);
            inputActions = (InputActionAsset)EditorGUILayout.ObjectField("Input Actions Asset", inputActions, typeof(InputActionAsset), false);
            heroAddWeapon = EditorGUILayout.Toggle("Add Hero Melee Weapon", heroAddWeapon);
            heroTryAutoWeapon = EditorGUILayout.Toggle("Auto-Find Weapon Transform", heroTryAutoWeapon);
            heroAutoCreateCamera = EditorGUILayout.Toggle("Create Camera If Missing", heroAutoCreateCamera);
            heroForceReplaceCamera = EditorGUILayout.Toggle("Force Replace Camera", heroForceReplaceCamera);
            heroAutoCreateAudio = EditorGUILayout.Toggle("Create Audio If Missing", heroAutoCreateAudio);
            heroTagAsPlayer = EditorGUILayout.Toggle("Tag As Player", heroTagAsPlayer);
            if (heroTagAsPlayer)
            {
                heroTag = EditorGUILayout.TextField("Hero Tag Name", heroTag);
            }
            heroOverrideLayer = EditorGUILayout.Toggle("Override Hero Layer", heroOverrideLayer);
            using (new EditorGUI.DisabledGroupScope(!heroOverrideLayer))
            {
                heroLayer = EditorGUILayout.LayerField("Hero Layer", heroLayer);
            }

            EditorGUILayout.Space(4f);
            heroCameraDistance = EditorGUILayout.FloatField("Camera Distance", heroCameraDistance);
            heroCameraPivotOffset = EditorGUILayout.FloatField("Camera Pivot Offset", heroCameraPivotOffset);
            heroCameraVerticalOffset = EditorGUILayout.FloatField("Camera Vertical Offset", heroCameraVerticalOffset);
            heroWalkSpeed = EditorGUILayout.FloatField("Walk Speed", heroWalkSpeed);
            heroSprintSpeed = EditorGUILayout.FloatField("Sprint Speed", heroSprintSpeed);
            heroGroundMask = LayerMaskField("Ground Mask", heroGroundMask);

            EditorGUILayout.Space(4f);
            heroMaxHealth = EditorGUILayout.FloatField("Hero Max Health", heroMaxHealth);
            heroAttackDamage = EditorGUILayout.FloatField("Hero Attack Damage", heroAttackDamage);
            heroAttackCooldown = EditorGUILayout.FloatField("Hero Attack Cooldown", heroAttackCooldown);
            heroBlockMultiplier = EditorGUILayout.Slider("Hero Block Multiplier", heroBlockMultiplier, 0f, 1f);
            heroPostHitInvuln = EditorGUILayout.FloatField("Post Hit Invulnerability", heroPostHitInvuln);
            heroDamageVariance = EditorGUILayout.Slider("Hero Damage Variance (+/-)", heroDamageVariance, 0f, 1f);
            heroAttachFloatingText = EditorGUILayout.Toggle("Floating Damage Text", heroAttachFloatingText);
            using (new EditorGUI.DisabledGroupScope(!heroAttachFloatingText))
            {
                heroFloatingTextOffset = EditorGUILayout.Vector3Field("Floating Text Offset", heroFloatingTextOffset);
                heroFloatingTextColor = EditorGUILayout.ColorField("Floating Text Color", heroFloatingTextColor);
            }

            EditorGUILayout.Space(4f);
            heroMeleeRadius = EditorGUILayout.FloatField("Weapon Radius", heroMeleeRadius);
            heroMeleeAngle = EditorGUILayout.FloatField("Weapon Angle", heroMeleeAngle);
            heroMeleeDamageMultiplier = EditorGUILayout.FloatField("Weapon Damage Multiplier", heroMeleeDamageMultiplier);
            heroMeleeMask = LayerMaskField("Weapon Hit Mask", heroMeleeMask);
            heroMeleeIncludeTriggers = EditorGUILayout.Toggle("Weapon Hits Triggers", heroMeleeIncludeTriggers);
            heroHideRigHierarchy = EditorGUILayout.Toggle("Hide Bone Hierarchy", heroHideRigHierarchy);

            showAnimationFoldout = EditorGUILayout.Foldout(showAnimationFoldout, "Animator Parameter Mapping", true);
            if (showAnimationFoldout)
            {
                EditorGUI.indentLevel++;
                animationProfile.velocityFloat = EditorGUILayout.TextField("Velocity Float", animationProfile.velocityFloat);
                animationProfile.forwardVelocityFloat = EditorGUILayout.TextField("Forward Float", animationProfile.forwardVelocityFloat);
                animationProfile.strafeVelocityFloat = EditorGUILayout.TextField("Strafe Float", animationProfile.strafeVelocityFloat);
                animationProfile.groundedBool = EditorGUILayout.TextField("Grounded Bool", animationProfile.groundedBool);
                animationProfile.sprintBool = EditorGUILayout.TextField("Sprint Bool", animationProfile.sprintBool);
                animationProfile.idleBool = EditorGUILayout.TextField("Idle Bool", animationProfile.idleBool);
                animationProfile.jumpTrigger = EditorGUILayout.TextField("Jump Trigger", animationProfile.jumpTrigger);
                animationProfile.landTrigger = EditorGUILayout.TextField("Land Trigger", animationProfile.landTrigger);
                animationProfile.attackTrigger = EditorGUILayout.TextField("Attack Trigger", animationProfile.attackTrigger);
                animationProfile.damageTrigger = EditorGUILayout.TextField("Damage Trigger", animationProfile.damageTrigger);
                animationProfile.deathTrigger = EditorGUILayout.TextField("Death Trigger", animationProfile.deathTrigger);
                animationProfile.applyRootMotion = EditorGUILayout.Toggle("Apply Root Motion", animationProfile.applyRootMotion);
                animationProfile.attackStateName = EditorGUILayout.TextField("Attack State Name", animationProfile.attackStateName);
                animationProfile.attackStateLayer = EditorGUILayout.IntField("Attack State Layer", animationProfile.attackStateLayer);
                animationProfile.attackWindowStart = EditorGUILayout.Slider("Attack Window Start", animationProfile.attackWindowStart, 0f, 1f);
                animationProfile.attackWindowEnd = EditorGUILayout.Slider("Attack Window End", animationProfile.attackWindowEnd, 0f, 1f);
                EditorGUI.indentLevel--;
            }

            if (GUILayout.Button("Configure Hero", GUILayout.Height(28f)))
            {
                if (heroInstance == null)
                {
                    ShowReport("Assign a hero GameObject in the scene.", MessageType.Warning);
                }
                else
                {
                    var options = new HeroAutoSetupOptions
                    {
                        cameraOverride = heroCameraOverride,
                        animatorOverride = heroAnimatorOverride,
                        audioOverride = heroAudioOverride,
                        attackOrigin = heroWeaponOverride,
                        addMeleeWeapon = heroAddWeapon,
                        tryAutoFindWeapon = heroTryAutoWeapon,
                        autoCreateCamera = heroAutoCreateCamera,
                        forceReplaceCamera = heroForceReplaceCamera,
                        autoCreateAudio = heroAutoCreateAudio,
                        tagAsPlayer = heroTagAsPlayer,
                        playerTag = heroTag,
                        cameraDistance = Mathf.Max(0.5f, heroCameraDistance),
                        cameraPivotOffset = heroCameraPivotOffset,
                        cameraVerticalOffset = heroCameraVerticalOffset,
                        walkSpeed = Mathf.Max(0.1f, heroWalkSpeed),
                        sprintSpeed = Mathf.Max(0.2f, heroSprintSpeed),
                        groundMask = heroGroundMask,
                        heroMaxHealth = Mathf.Max(1f, heroMaxHealth),
                        heroAttackDamage = Mathf.Max(0f, heroAttackDamage),
                        heroAttackCooldown = Mathf.Max(0f, heroAttackCooldown),
                        heroBlockMultiplier = Mathf.Clamp01(heroBlockMultiplier),
                        heroPostHitInvulnerability = Mathf.Max(0f, heroPostHitInvuln),
                        heroDamageVariance = Mathf.Clamp01(heroDamageVariance),
                        meleeRadius = Mathf.Max(0.1f, heroMeleeRadius),
                        meleeAngle = Mathf.Clamp(heroMeleeAngle, 1f, 360f),
                        meleeDamageMultiplier = Mathf.Max(0f, heroMeleeDamageMultiplier),
                        meleeHitMask = heroMeleeMask,
                        meleeIncludeTriggers = heroMeleeIncludeTriggers,
                        inputActions = inputActions,
                        animationBindings = animationProfile,
                        overrideHeroLayer = heroOverrideLayer,
                        heroLayer = heroLayer,
                        hideRigHierarchy = heroHideRigHierarchy,
                        attachFloatingCombatText = heroAttachFloatingText,
                        floatingCombatTextOffset = heroFloatingTextOffset,
                        floatingCombatTextColor = heroFloatingTextColor
                    };

                    var result = HeroCombatAutoWireUtility.ConfigureHero(heroInstance, options);
                    if (result.Report != null)
                    {
                        ShowReport(result.Report.BuildSummary(), result.Report.HasErrors ? MessageType.Error : MessageType.Info);
                    }
                }
            }
        }

        void DrawNpcSection()
        {
            EditorGUILayout.LabelField("NPC Setup", EditorStyles.boldLabel);
            npcInstance = (GameObject)EditorGUILayout.ObjectField("NPC In Scene", npcInstance, typeof(GameObject), true);
            heroTargetOverride = (HeroCharacterController)EditorGUILayout.ObjectField("Hero Target Override", heroTargetOverride, typeof(HeroCharacterController), true);
            npcWeaponOverride = (Transform)EditorGUILayout.ObjectField("NPC Weapon / Attack Origin", npcWeaponOverride, typeof(Transform), true);
            npcTryAutoWeapon = EditorGUILayout.Toggle("Auto-Find Weapon Transform", npcTryAutoWeapon);
            npcMeleeRadius = EditorGUILayout.FloatField("Weapon Radius", npcMeleeRadius);
            npcMeleeAngle = EditorGUILayout.FloatField("Weapon Angle", npcMeleeAngle);
            npcMeleeDamageMultiplier = EditorGUILayout.FloatField("Weapon Damage Multiplier", npcMeleeDamageMultiplier);
            npcMeleeMask = LayerMaskField("Weapon Hit Mask", npcMeleeMask);
            npcMeleeIncludeTriggers = EditorGUILayout.Toggle("Weapon Hits Triggers", npcMeleeIncludeTriggers);
            npcAddCollider = EditorGUILayout.Toggle("Ensure Capsule Collider", npcAddCollider);
            npcTagAsEnemy = EditorGUILayout.Toggle("Tag As Enemy", npcTagAsEnemy);
            if (npcTagAsEnemy)
            {
                npcTag = EditorGUILayout.TextField("Enemy Tag Name", npcTag);
            }
            npcOverrideLayer = EditorGUILayout.Toggle("Override NPC Layer", npcOverrideLayer);
            using (new EditorGUI.DisabledGroupScope(!npcOverrideLayer))
            {
                npcLayer = EditorGUILayout.LayerField("NPC Layer", npcLayer);
            }
            npcMaxHealth = EditorGUILayout.FloatField("NPC Max Health", npcMaxHealth);
            npcAttackDamage = EditorGUILayout.FloatField("NPC Attack Damage", npcAttackDamage);
            npcAttackCooldown = EditorGUILayout.FloatField("NPC Attack Cooldown", npcAttackCooldown);
            npcAcquireRange = EditorGUILayout.FloatField("Acquire Range", npcAcquireRange);
            npcAttackRange = EditorGUILayout.FloatField("Attack Range", npcAttackRange);
            npcTurnSpeed = EditorGUILayout.FloatField("Turn Speed", npcTurnSpeed);
            npcDamageScale = EditorGUILayout.FloatField("Damage Scale", npcDamageScale);
            npcLeashRange = EditorGUILayout.FloatField("Leash (Aggro) Range", npcLeashRange);
            npcReturnSpeed = EditorGUILayout.FloatField("Return Speed", npcReturnSpeed);
            npcAttackDelay = EditorGUILayout.FloatField("Attack Delay", npcAttackDelay);
            npcAttackStateName = EditorGUILayout.TextField("Attack State Name", npcAttackStateName);
            npcAttackLayer = EditorGUILayout.IntField("Attack State Layer", npcAttackLayer);
            npcAttackWindowStart = EditorGUILayout.Slider("Attack Window Start", npcAttackWindowStart, 0f, 1f);
            npcAttackWindowEnd = EditorGUILayout.Slider("Attack Window End", npcAttackWindowEnd, 0f, 1f);
            npcDamageVariance = EditorGUILayout.Slider("Damage Variance (+/-)", npcDamageVariance, 0f, 1f);
            npcAttachHealthBar = EditorGUILayout.Toggle("Attach Health Bar", npcAttachHealthBar);
            using (new EditorGUI.DisabledGroupScope(!npcAttachHealthBar))
            {
                npcHealthBarOffset = EditorGUILayout.Vector3Field("Health Bar Offset", npcHealthBarOffset);
                npcHealthBarSize = EditorGUILayout.Vector2Field("Health Bar Size", npcHealthBarSize);
                npcHealthBarScale = EditorGUILayout.FloatField("Health Bar Scale", npcHealthBarScale);
                npcHealthBarAlwaysVisible = EditorGUILayout.Toggle("Health Bar Always Visible", npcHealthBarAlwaysVisible);
            }
            npcAttachFloatingText = EditorGUILayout.Toggle("Floating Damage Text", npcAttachFloatingText);
            using (new EditorGUI.DisabledGroupScope(!npcAttachFloatingText))
            {
                npcFloatingTextOffset = EditorGUILayout.Vector3Field("Floating Text Offset", npcFloatingTextOffset);
                npcFloatingTextColor = EditorGUILayout.ColorField("Floating Text Color", npcFloatingTextColor);
            }
            npcEnableMovement = EditorGUILayout.Toggle("Enable Movement", npcEnableMovement);
            using (new EditorGUI.DisabledGroupScope(!npcEnableMovement))
            {
                npcAllowSprint = EditorGUILayout.Toggle("Allow Sprinting", npcAllowSprint);
                npcWalkSpeed = EditorGUILayout.FloatField("Walk Speed", npcWalkSpeed);
                npcSprintSpeed = EditorGUILayout.FloatField("Sprint Speed", npcSprintSpeed);
                npcSprintDistance = EditorGUILayout.FloatField("Sprint Distance Threshold", npcSprintDistance);
                npcStoppingBuffer = EditorGUILayout.FloatField("Stopping Buffer", npcStoppingBuffer);
            }
            npcColliderRadius = EditorGUILayout.FloatField("Collider Radius", npcColliderRadius);
            npcColliderHeight = EditorGUILayout.FloatField("Collider Height", npcColliderHeight);

            if (GUILayout.Button("Configure NPC", GUILayout.Height(28f)))
            {
                if (npcInstance == null)
                {
                    ShowReport("Assign an NPC GameObject in the scene.", MessageType.Warning);
                }
                else
                {
                    var options = new NpcAutoSetupOptions
                    {
                        heroTarget = heroTargetOverride,
                        addCapsuleCollider = npcAddCollider,
                        tagAsEnemy = npcTagAsEnemy,
                        enemyTag = npcTag,
                        enemyLayer = npcLayer,
                        overrideEnemyLayer = npcOverrideLayer,
                        maxHealth = Mathf.Max(1f, npcMaxHealth),
                        attackDamage = Mathf.Max(0f, npcAttackDamage),
                        attackCooldown = Mathf.Max(0f, npcAttackCooldown),
                        acquireRange = Mathf.Max(0.1f, npcAcquireRange),
                        attackRange = Mathf.Max(0.1f, npcAttackRange),
                        turnSpeed = Mathf.Max(0.1f, npcTurnSpeed),
                        damageScale = Mathf.Max(0f, npcDamageScale),
                        leashRange = Mathf.Max(0.1f, npcLeashRange),
                        returnSpeed = Mathf.Max(0f, npcReturnSpeed),
                        attackDelay = Mathf.Max(0f, npcAttackDelay),
                        meleeRadius = Mathf.Max(0.05f, npcMeleeRadius),
                        meleeAngle = Mathf.Clamp(npcMeleeAngle, 1f, 360f),
                        meleeDamageMultiplier = Mathf.Max(0f, npcMeleeDamageMultiplier),
                        meleeHitMask = npcMeleeMask,
                        meleeIncludeTriggers = npcMeleeIncludeTriggers,
                        attackStateName = npcAttackStateName,
                        attackLayer = npcAttackLayer,
                        attackWindowStart = Mathf.Clamp01(npcAttackWindowStart),
                        attackWindowEnd = Mathf.Clamp01(npcAttackWindowEnd),
                        enableMovement = npcEnableMovement,
                        allowSprint = npcAllowSprint,
                        walkSpeed = Mathf.Max(0f, npcWalkSpeed),
                        sprintSpeed = Mathf.Max(0f, npcSprintSpeed),
                        sprintDistance = Mathf.Max(0f, npcSprintDistance),
                        stoppingBuffer = Mathf.Max(0f, npcStoppingBuffer),
                        colliderRadius = Mathf.Max(0.05f, npcColliderRadius),
                        colliderHeight = Mathf.Max(0.2f, npcColliderHeight),
                        weaponOverride = npcWeaponOverride,
                        tryAutoFindWeapon = npcTryAutoWeapon,
                        damageVariance = Mathf.Clamp01(npcDamageVariance),
                        attachHealthBar = npcAttachHealthBar,
                        healthBarOffset = npcHealthBarOffset,
                        healthBarSize = npcHealthBarSize,
                        healthBarScale = Mathf.Max(0.01f, npcHealthBarScale),
                        healthBarAlwaysVisible = npcHealthBarAlwaysVisible,
                        attachFloatingText = npcAttachFloatingText,
                        floatingTextOffset = npcFloatingTextOffset,
                        floatingTextColor = npcFloatingTextColor
                    };

                    var result = HeroCombatAutoWireUtility.ConfigureNpc(npcInstance, options);
                    if (result.Report != null)
                    {
                        ShowReport(result.Report.BuildSummary(), result.Report.HasErrors ? MessageType.Error : MessageType.Info);
                    }
                }
            }
        }

        void ShowReport(string message, MessageType type)
        {
            lastReport = message;
            lastReportType = type;
            Repaint();
        }

        static LayerMask LayerMaskField(string label, LayerMask layerMask)
        {
            var layers = UnityEditorInternal.InternalEditorUtility.layers;
            int mask = layerMask.value;
            int maskWithoutEmpty = 0;
            for (int i = 0; i < layers.Length; i++)
            {
                int layer = LayerMask.NameToLayer(layers[i]);
                if (layer < 0)
                {
                    continue;
                }

                if ((mask & (1 << layer)) != 0)
                {
                    maskWithoutEmpty |= 1 << i;
                }
            }

            int newMask = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers);
            int newValue = 0;
            for (int i = 0; i < layers.Length; i++)
            {
                if ((newMask & (1 << i)) == 0)
                {
                    continue;
                }

                int layer = LayerMask.NameToLayer(layers[i]);
                if (layer >= 0)
                {
                    newValue |= 1 << layer;
                }
            }

            layerMask.value = newValue;
            return layerMask;
        }
    }

    [Serializable]
        public class AnimationBindingProfile
        {
            public string velocityFloat = "Vel";
            public string forwardVelocityFloat = "";
            public string strafeVelocityFloat = "";
            public string groundedBool = "Grounded";
            public string sprintBool = "Sprinting";
            public string idleBool = "Idle";
            public string jumpTrigger = "Jump";
            public string landTrigger = "";
            public string attackTrigger = "Attacking";
            public string damageTrigger = "Damage";
            public string deathTrigger = "Death";
            public bool applyRootMotion;
            public string attackStateName = "Attack";
            public int attackStateLayer = 1;
            [Range(0f, 1f)] public float attackWindowStart = 0.3f;
            [Range(0f, 1f)] public float attackWindowEnd = 0.45f;

            public static AnimationBindingProfile CreateDefault()
            {
                return new AnimationBindingProfile();
            }
        }

    public class HeroAutoSetupOptions
    {
        public Camera cameraOverride;
        public Animator animatorOverride;
        public AudioSource audioOverride;
        public Transform attackOrigin;
        public bool addMeleeWeapon = true;
        public bool tryAutoFindWeapon = true;
        public bool autoCreateCamera = true;
        public bool forceReplaceCamera;
        public bool autoCreateAudio = true;
        public bool tagAsPlayer = true;
        public string playerTag = "Player";
        public bool overrideHeroLayer;
        public int heroLayer = 0;
        public float cameraDistance = 3f;
        public float cameraPivotOffset = -0.5f;
        public float cameraVerticalOffset = 0f;
        public float walkSpeed = 3.5f;
        public float sprintSpeed = 6f;
        public LayerMask groundMask = ~0;
        public float heroMaxHealth = 100f;
        public float heroAttackDamage = 25f;
        public float heroAttackCooldown = 0.6f;
        public float heroBlockMultiplier = 0.4f;
        public float heroPostHitInvulnerability = 0.15f;
        public float heroDamageVariance = 0.3f;
        public float meleeRadius = 2f;
        public float meleeAngle = 120f;
        public float meleeDamageMultiplier = 1f;
        public LayerMask meleeHitMask = ~0;
        public bool meleeIncludeTriggers;
        public InputActionAsset inputActions;
        public AnimationBindingProfile animationBindings = AnimationBindingProfile.CreateDefault();
        public bool hideRigHierarchy = true;
        public bool attachFloatingCombatText = true;
        public Vector3 floatingCombatTextOffset = new Vector3(0f, 2f, 0f);
        public Color floatingCombatTextColor = new Color(0.8f, 0.2f, 0.2f, 1f);
    }

    public class NpcAutoSetupOptions
    {
        public Transform weaponOverride;
        public bool tryAutoFindWeapon = true;
        public HeroCharacterController heroTarget;
        public bool addCapsuleCollider = true;
        public bool tagAsEnemy = true;
        public string enemyTag = "Enemy";
        public int enemyLayer = -1;
        public bool overrideEnemyLayer;
        public float maxHealth = 100f;
        public float attackDamage = 10f;
        public float attackCooldown = 0.6f;
        public float acquireRange = 5f;
        public float attackRange = 1.9f;
        public float turnSpeed = 8f;
        public float damageScale = 1f;
        public float leashRange = 10f;
        public float returnSpeed = 3f;
        public float attackDelay = 1f;
        public float damageVariance = 0.3f;
        public bool enableMovement = true;
        public bool allowSprint = true;
        public float walkSpeed = 1.5f;
        public float sprintSpeed = 3f;
        public float sprintDistance = 3f;
        public float stoppingBuffer = 0.25f;
        public float colliderRadius = 0.4f;
        public float colliderHeight = 1.8f;
        public float meleeRadius = 1.5f;
        public float meleeAngle = 120f;
        public float meleeDamageMultiplier = 1f;
        public LayerMask meleeHitMask = ~0;
        public bool meleeIncludeTriggers;
        public string attackStateName = "Attack";
        public int attackLayer = 1;
        public float attackWindowStart = 0.3f;
        public float attackWindowEnd = 0.45f;
        public bool attachHealthBar = true;
        public Vector3 healthBarOffset = new Vector3(0f, 1.6f, 0f);
        public Vector2 healthBarSize = new Vector2(1.2f, 0.18f);
        public float healthBarScale = 1f;
        public bool healthBarAlwaysVisible;
        public bool attachFloatingText = true;
        public Vector3 floatingTextOffset = new Vector3(0f, 2.1f, 0f);
        public Color floatingTextColor = new Color(0.235f, 0.776f, 0.851f, 1f);
    }

    public class HeroSetupResult
    {
        public HeroCharacterController Controller;
        public CharacterCombatAgent CombatAgent;
        public MeleeWeapon Weapon;
        public PlayerInput PlayerInput;
        public Camera Camera;
        public SetupReport Report;
    }

    public class NpcSetupResult
    {
        public NpcCombatController Controller;
        public CharacterCombatAgent CombatAgent;
        public Collider Collider;
        public SetupReport Report;
    }

    public class SetupReport
    {
        readonly List<string> actions = new List<string>();
        readonly List<string> warnings = new List<string>();
        readonly List<string> errors = new List<string>();

        public bool HasErrors => errors.Count > 0;

        public void AddAction(string message) => actions.Add(message);
        public void AddWarning(string message) => warnings.Add(message);
        public void AddError(string message) => errors.Add(message);

        public string BuildSummary()
        {
            var sb = new StringBuilder();
            foreach (var action in actions)
            {
                sb.AppendLine($"• {action}");
            }

            foreach (var warning in warnings)
            {
                sb.AppendLine($"! {warning}");
            }

            foreach (var error in errors)
            {
                sb.AppendLine($"✖ {error}");
            }

            return sb.ToString().Trim();
        }
    }

    public static class HeroCombatAutoWireUtility
    {
        static readonly string[] WeaponKeywords = { "sword", "weapon", "blade", "axe", "hand_r", "r_hand", "right_hand", "righthand", "mixamorig:righthand" };
        const string HeroAnimatorPath = "Packages/com.herocharacter.herocombat/Runtime/Animation/HeroCombatAnimator.controller";

        public static GameObject InstantiateAsset(GameObject asset, Vector3 position)
        {
            if (asset == null)
            {
                return null;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (instance == null)
            {
                Debug.LogWarning($"Failed to instantiate asset '{asset.name}'.");
                return null;
            }

            Undo.RegisterCreatedObjectUndo(instance, $"Instantiate {asset.name}");
            instance.transform.position = position;
            SceneView.lastActiveSceneView?.FrameSelected();
            return instance;
        }

        public static InputActionAsset FindDefaultInputAsset()
        {
            string[] guids = AssetDatabase.FindAssets("t:InputActionAsset");
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("InputSystem_Actions.inputactions", StringComparison.OrdinalIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                }
            }

            if (guids.Length > 0)
            {
                string fallbackPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<InputActionAsset>(fallbackPath);
            }

            return null;
        }

        public static HeroSetupResult ConfigureHero(GameObject heroRoot, HeroAutoSetupOptions options)
        {
            var result = new HeroSetupResult { Report = new SetupReport() };
            if (heroRoot == null)
            {
                result.Report.AddError("Hero root is null.");
                return result;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Configure Hero Combat");
            var controller = heroRoot.GetComponent<HeroCharacterController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<HeroCharacterController>(heroRoot);
                result.Report.AddAction("Added HeroCharacterController.");
            }
            else
            {
                result.Report.AddAction("Reused existing HeroCharacterController.");
            }
            result.Controller = controller;

            var combatAgent = heroRoot.GetComponent<CharacterCombatAgent>();
            if (combatAgent == null)
            {
                combatAgent = Undo.AddComponent<CharacterCombatAgent>(heroRoot);
                result.Report.AddAction("Added CharacterCombatAgent.");
            }
            result.CombatAgent = combatAgent;

            var body = heroRoot.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = Undo.AddComponent<Rigidbody>(heroRoot);
                result.Report.AddAction("Added Rigidbody.");
            }
            Undo.RecordObject(body, "Configure Hero Rigidbody");
            body.useGravity = true;
            body.linearDamping = 0f;
            body.angularDamping = 0.05f;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.None;
            EditorUtility.SetDirty(body);

            var capsule = heroRoot.GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                capsule = Undo.AddComponent<CapsuleCollider>(heroRoot);
                result.Report.AddAction("Added CapsuleCollider.");
            }
            ConfigureCapsule(capsule, heroRoot, result.Report);

            AudioSource audioSource = options.audioOverride;
            if (audioSource == null)
            {
                audioSource = heroRoot.GetComponent<AudioSource>();
            }

            if (audioSource == null && options.autoCreateAudio)
            {
                audioSource = Undo.AddComponent<AudioSource>(heroRoot);
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
                result.Report.AddAction("Added AudioSource for footsteps.");
            }

            if (options.forceReplaceCamera)
            {
                RemoveChildCameras(heroRoot, result.Report);
            }

            Camera camera = options.cameraOverride;
            if (camera == null)
            {
                camera = heroRoot.GetComponentInChildren<Camera>(true);
            }

            if (camera == null && options.autoCreateCamera)
            {
                var cameraGO = new GameObject("HeroCamera");
                Undo.RegisterCreatedObjectUndo(cameraGO, "Create Hero Camera");
                cameraGO.transform.SetParent(heroRoot.transform);
                cameraGO.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                cameraGO.transform.localRotation = Quaternion.identity;
                camera = cameraGO.AddComponent<Camera>();
                camera.nearClipPlane = 0.1f;
                camera.tag = "MainCamera";
                cameraGO.AddComponent<AudioListener>();
                result.Report.AddAction("Created child camera.");
            }
            result.Camera = camera;

            var animator = options.animatorOverride;
            if (animator == null)
            {
                animator = heroRoot.GetComponentInChildren<Animator>();
            }

            if (animator == null)
            {
                result.Report.AddWarning("No Animator found under hero root. Assign one manually in the Animation section.");
            }

            PlayerInput playerInput = heroRoot.GetComponent<PlayerInput>();
            if (playerInput == null)
            {
                playerInput = Undo.AddComponent<PlayerInput>(heroRoot);
                result.Report.AddAction("Added PlayerInput.");
            }
            result.PlayerInput = playerInput;
            if (options.inputActions != null)
            {
                Undo.RecordObject(playerInput, "Assign PlayerInput Actions");
                playerInput.actions = options.inputActions;
                playerInput.defaultActionMap = string.Empty;
                EditorUtility.SetDirty(playerInput);
            }
            else
            {
                result.Report.AddWarning("No InputActionAsset assigned. Drag one into the Input field so the controller can bind move/look actions.");
            }

            if (options.tagAsPlayer && !string.IsNullOrEmpty(options.playerTag))
            {
                if (EnsureTagExists(options.playerTag, result.Report))
                {
                    heroRoot.tag = options.playerTag;
                }
                else
                {
                    result.Report.AddWarning($"Failed to ensure tag '{options.playerTag}' exists.");
                }
            }

            if (options.overrideHeroLayer)
            {
                heroRoot.layer = options.heroLayer;
            }

            var heroFloatingTextSpawner = ConfigureFloatingCombatTextSpawner(heroRoot, options.attachFloatingCombatText, options.floatingCombatTextOffset, options.floatingCombatTextColor, result.Report);
            ConfigureHeroSerializedFields(controller, camera, animator, audioSource, capsule, playerInput, options, result.Report, heroFloatingTextSpawner);
            ConfigureCombatAgent(combatAgent, options.heroMaxHealth, options.heroAttackDamage, options.heroAttackCooldown, options.heroBlockMultiplier, options.heroPostHitInvulnerability, options.heroDamageVariance, result.Report);

            if (options.addMeleeWeapon)
            {
                var attackOrigin = options.attackOrigin;
                if (attackOrigin == null && options.tryAutoFindWeapon)
                {
                    attackOrigin = FindTransformByKeywords(heroRoot, WeaponKeywords);
                    if (attackOrigin != null)
                    {
                        result.Report.AddAction($"Auto-selected '{attackOrigin.name}' as the hero weapon.");
                    }
                }

                if (attackOrigin == null)
                {
                    result.Report.AddWarning("No attack origin assigned. Drag a sword or hand bone into the Weapon field if you want melee collisions configured.");
                }
                else
                {
                    var weapon = attackOrigin.GetComponent<MeleeWeapon>();
                    if (weapon == null)
                    {
                        weapon = Undo.AddComponent<MeleeWeapon>(attackOrigin.gameObject);
                        result.Report.AddAction($"Added MeleeWeapon to '{attackOrigin.name}'.");
                    }

                    var weaponSO = new SerializedObject(weapon);
                    weaponSO.FindProperty("owner").objectReferenceValue = controller.Combat;
                    weaponSO.FindProperty("ownerTransform").objectReferenceValue = controller.transform;
                    weaponSO.FindProperty("attackOrigin").objectReferenceValue = attackOrigin;
                    weaponSO.FindProperty("attackRadius").floatValue = options.meleeRadius;
                    weaponSO.FindProperty("attackAngle").floatValue = options.meleeAngle;
                    weaponSO.FindProperty("damageMultiplier").floatValue = options.meleeDamageMultiplier;
                    weaponSO.FindProperty("includeTriggers").boolValue = options.meleeIncludeTriggers;
                    weaponSO.FindProperty("hitMask").intValue = options.meleeHitMask.value;
                    weaponSO.ApplyModifiedProperties();
                    EditorUtility.SetDirty(weapon);
                    weapon.SetOwner(controller.Combat);
                    result.Weapon = weapon;

                    var runtimeAnimator = options.animatorOverride != null ? options.animatorOverride : controller.GetComponentInChildren<Animator>();
                    ConfigureAttackWindowDriver(
                        controller.gameObject,
                        runtimeAnimator,
                        controller.Combat,
                        weapon,
                        options.animationBindings.attackStateName,
                        options.animationBindings.attackStateLayer,
                        options.animationBindings.attackWindowStart,
                        options.animationBindings.attackWindowEnd,
                        result.Report);
                }
            }

            if (heroRoot.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(heroRoot.scene);
            }

            SetRigHierarchyHidden(heroRoot, options.hideRigHierarchy, result.Report);

            Undo.CollapseUndoOperations(undoGroup);
            return result;
        }

        public static NpcSetupResult ConfigureNpc(GameObject npcRoot, NpcAutoSetupOptions options)
        {
            var result = new NpcSetupResult { Report = new SetupReport() };
            if (npcRoot == null)
            {
                result.Report.AddError("NPC root is null.");
                return result;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Configure NPC Combat");
            var combatAgent = npcRoot.GetComponent<CharacterCombatAgent>();
            if (combatAgent == null)
            {
                combatAgent = Undo.AddComponent<CharacterCombatAgent>(npcRoot);
                result.Report.AddAction("Added CharacterCombatAgent to NPC.");
            }
            result.CombatAgent = combatAgent;
            ConfigureCombatAgent(combatAgent, options.maxHealth, options.attackDamage, options.attackCooldown, 1f, 0f, options.damageVariance, result.Report);
            ConfigureNpcHealthBar(npcRoot, combatAgent, options, result.Report);
            ConfigureFloatingCombatTextSpawner(npcRoot, options.attachFloatingText, options.floatingTextOffset, options.floatingTextColor, result.Report);

            var npcController = npcRoot.GetComponent<NpcCombatController>();
            if (npcController == null)
            {
                npcController = Undo.AddComponent<NpcCombatController>(npcRoot);
                result.Report.AddAction("Added NpcCombatController.");
            }
            result.Controller = npcController;
            var npcSO = new SerializedObject(npcController);
            bool hasExplicitHero = options.heroTarget != null;
            npcSO.FindProperty("heroTargetOverride").objectReferenceValue = options.heroTarget;
            npcSO.FindProperty("autoTargetHero").boolValue = !hasExplicitHero;
            npcSO.FindProperty("acquireRange").floatValue = options.acquireRange;
            npcSO.FindProperty("attackRange").floatValue = options.attackRange;
            npcSO.FindProperty("turnSpeed").floatValue = options.turnSpeed;
            npcSO.FindProperty("damageScale").floatValue = options.damageScale;
            npcSO.FindProperty("leashRange").floatValue = options.leashRange;
            npcSO.FindProperty("returnSpeed").floatValue = options.returnSpeed;
            npcSO.FindProperty("attackDelay").floatValue = options.attackDelay;
            npcSO.FindProperty("enableMovement").boolValue = options.enableMovement;
            npcSO.FindProperty("allowSprint").boolValue = options.allowSprint;
            npcSO.FindProperty("walkSpeed").floatValue = options.walkSpeed;
            npcSO.FindProperty("sprintSpeed").floatValue = options.sprintSpeed;
            npcSO.FindProperty("sprintDistance").floatValue = options.sprintDistance;
            npcSO.FindProperty("stoppingBuffer").floatValue = options.stoppingBuffer;
            npcSO.FindProperty("attachHealthBar").boolValue = options.attachHealthBar;
            npcSO.FindProperty("healthBarOffset").vector3Value = options.healthBarOffset;
            npcSO.FindProperty("healthBarSize").vector2Value = options.healthBarSize;
            npcSO.FindProperty("healthBarScale").floatValue = options.healthBarScale;
            npcSO.FindProperty("healthBarAlwaysVisible").boolValue = options.healthBarAlwaysVisible;
            npcSO.FindProperty("attachFloatingText").boolValue = options.attachFloatingText;
            npcSO.FindProperty("floatingTextOffset").vector3Value = options.floatingTextOffset;
            npcSO.FindProperty("floatingTextColor").colorValue = options.floatingTextColor;
            npcSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(npcController);

            var animatorDriver = npcRoot.GetComponent<NpcAnimatorDriver>();
            if (animatorDriver == null)
            {
                animatorDriver = Undo.AddComponent<NpcAnimatorDriver>(npcRoot);
                result.Report.AddAction("Added NpcAnimatorDriver.");
            }

            var driverSO = new SerializedObject(animatorDriver);
            var animatorProp = driverSO.FindProperty("animator");
            if (animatorProp != null && animatorProp.objectReferenceValue == null)
            {
                var animator = npcRoot.GetComponentInChildren<Animator>();
                animatorProp.objectReferenceValue = animator;
                if (animator == null)
                {
                    result.Report.AddWarning("NPC hierarchy has no Animator; animations/attacks will be invisible until one is assigned.");
                }
            }

            var bodyProp = driverSO.FindProperty("body");
            if (bodyProp != null && bodyProp.objectReferenceValue == null)
            {
                var rb = npcRoot.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    bodyProp.objectReferenceValue = rb;
                }
            }
            driverSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(animatorDriver);

            Transform weaponOverride = options.weaponOverride;
            if (weaponOverride == null && options.tryAutoFindWeapon)
            {
                weaponOverride = FindTransformByKeywords(npcRoot, WeaponKeywords);
                if (weaponOverride != null)
                {
                    result.Report.AddAction($"Auto-selected '{weaponOverride.name}' as the NPC weapon.");
                }
            }

            if (weaponOverride != null)
            {
                ConfigureNpcWeapon(npcRoot, weaponOverride, combatAgent, options, result.Report);
            }

            Collider collider = npcRoot.GetComponent<Collider>();
            if (collider == null && options.addCapsuleCollider)
            {
                var capsule = Undo.AddComponent<CapsuleCollider>(npcRoot);
                capsule.direction = 1;
                capsule.radius = options.colliderRadius;
                capsule.height = options.colliderHeight;
                capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
                collider = capsule;
                result.Report.AddAction("Added CapsuleCollider to NPC for hit detection.");
            }
            result.Collider = collider;

            if (options.tagAsEnemy && !string.IsNullOrEmpty(options.enemyTag))
            {
                if (EnsureTagExists(options.enemyTag, result.Report))
                {
                    npcRoot.tag = options.enemyTag;
                }
                else
                {
                    result.Report.AddWarning($"Failed to ensure tag '{options.enemyTag}' exists.");
                }
            }

            if (options.overrideEnemyLayer && options.enemyLayer >= 0)
            {
                npcRoot.layer = options.enemyLayer;
            }

            if (npcRoot.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(npcRoot.scene);
            }

            Undo.CollapseUndoOperations(undoGroup);
            return result;
        }

        static void ConfigureHeroSerializedFields(
            HeroCharacterController controller,
            Camera camera,
            Animator animator,
            AudioSource audioSource,
            CapsuleCollider capsule,
            PlayerInput playerInput,
            HeroAutoSetupOptions options,
            SetupReport report,
            FloatingCombatTextSpawner floatingTextSpawner)
        {
            var serializedController = new SerializedObject(controller);
            serializedController.Update();

            AssignObject(serializedController, "cameraSettings.playerCamera", camera, report, "camera");
            SetFloat(serializedController, "cameraSettings.thirdPersonDistance", options.cameraDistance);
            SetFloat(serializedController, "cameraSettings.thirdPersonPivotOffset", options.cameraPivotOffset);
            SetFloat(serializedController, "cameraSettings.thirdPersonVerticalOffset", options.cameraVerticalOffset);
            AssignObject(serializedController, "footsteps.audioSource", audioSource, report, "footstep audio source");
            SetFloat(serializedController, "movement.standingHeight", Mathf.Max(1f, capsule.height));
            SetFloat(serializedController, "movement.walkingSpeed", options.walkSpeed);
            SetFloat(serializedController, "movement.sprintingSpeed", options.sprintSpeed);
            SetFloat(serializedController, "movement.groundingProbeDepth", Mathf.Max(0.05f, capsule.radius * 0.5f));
            SetInt(serializedController, "movement.whatIsGround", options.groundMask.value);

            if (animator != null)
            {
                AssignObject(serializedController, "animationSettings.thirdPersonAnimator", animator, report, "Animator");
            }
            SetString(serializedController, "animationSettings.velocityFloat", options.animationBindings.velocityFloat);
            SetString(serializedController, "animationSettings.forwardVelocityFloat", options.animationBindings.forwardVelocityFloat);
            SetString(serializedController, "animationSettings.strafeVelocityFloat", options.animationBindings.strafeVelocityFloat);
            SetString(serializedController, "animationSettings.groundedBool", options.animationBindings.groundedBool);
            SetString(serializedController, "animationSettings.sprintBool", options.animationBindings.sprintBool);
            SetString(serializedController, "animationSettings.idleBool", options.animationBindings.idleBool);
            SetString(serializedController, "animationSettings.jumpStartTrigger", options.animationBindings.jumpTrigger);
            SetString(serializedController, "animationSettings.jumpLandTrigger", options.animationBindings.landTrigger);
            SetString(serializedController, "animationSettings.attackTrigger", options.animationBindings.attackTrigger);
            SetString(serializedController, "animationSettings.damageTrigger", options.animationBindings.damageTrigger);
            SetString(serializedController, "animationSettings.deathTrigger", options.animationBindings.deathTrigger);
            SetBool(serializedController, "animationSettings.applyRootMotion", options.animationBindings.applyRootMotion);

            AssignObject(serializedController, "input.playerInput", playerInput, report, "PlayerInput");
            SetBool(serializedController, "input.autoBindFromPlayerInput", true);

            SetBool(serializedController, "attachFloatingCombatText", options.attachFloatingCombatText);
            SetVector3(serializedController, "floatingCombatTextOffset", options.floatingCombatTextOffset);
            SetColor(serializedController, "floatingCombatTextColor", options.floatingCombatTextColor);
            var floatingProp = serializedController.FindProperty("floatingCombatText");
            if (floatingProp != null)
            {
                floatingProp.objectReferenceValue = floatingTextSpawner;
            }

            serializedController.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
        }

        static void ConfigureCombatAgent(
            CharacterCombatAgent agent,
            float maxHealth,
            float attackDamage,
            float attackCooldown,
            float blockMultiplier,
            float postHitInvulnerability,
            float attackDamageVariance,
            SetupReport report)
        {
            var serializedAgent = new SerializedObject(agent);
            var settingsProp = serializedAgent.FindProperty("combatSettings");
            if (settingsProp != null)
            {
                settingsProp.FindPropertyRelative("maxHealth").floatValue = maxHealth;
                settingsProp.FindPropertyRelative("attackDamage").floatValue = attackDamage;
                settingsProp.FindPropertyRelative("attackCooldown").floatValue = attackCooldown;
                settingsProp.FindPropertyRelative("blockDamageMultiplier").floatValue = blockMultiplier;
                settingsProp.FindPropertyRelative("postHitInvulnerability").floatValue = postHitInvulnerability;
                settingsProp.FindPropertyRelative("attackDamageVariance").floatValue = Mathf.Clamp01(attackDamageVariance);
            }
            else
            {
                report.AddWarning("Could not access CharacterCombatAgent settings via serialization.");
            }

            serializedAgent.ApplyModifiedProperties();
            EditorUtility.SetDirty(agent);
        }

        static void ConfigureAttackWindowDriver(GameObject host, Animator animator, CharacterCombatAgent agent, MeleeWeapon weapon, string stateName, int layerIndex, float windowStart, float windowEnd, SetupReport report)
        {
            if (host == null || weapon == null)
            {
                return;
            }

            var driver = host.GetComponent<AnimatorAttackWindowDriver>();
            if (driver == null)
            {
                driver = Undo.AddComponent<AnimatorAttackWindowDriver>(host);
                report.AddAction("Added AnimatorAttackWindowDriver for melee timing.");
            }

            var driverSO = new SerializedObject(driver);
            driverSO.FindProperty("animator").objectReferenceValue = animator;
            driverSO.FindProperty("weapon").objectReferenceValue = weapon;
            driverSO.FindProperty("combatAgent").objectReferenceValue = agent;
            var windowsProp = driverSO.FindProperty("windows");
            if (windowsProp != null)
            {
                if (windowsProp.arraySize == 0)
                {
                    windowsProp.InsertArrayElementAtIndex(0);
                }

                var window = windowsProp.GetArrayElementAtIndex(0);
                window.FindPropertyRelative("stateName").stringValue = stateName;
                window.FindPropertyRelative("layerIndex").intValue = Mathf.Max(0, layerIndex);
                window.FindPropertyRelative("startNormalizedTime").floatValue = Mathf.Clamp01(windowStart);
                window.FindPropertyRelative("endNormalizedTime").floatValue = Mathf.Clamp01(Mathf.Max(windowStart, windowEnd));
            }

            driverSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(driver);
        }

        static void ConfigureNpcWeapon(GameObject npcRoot, Transform weaponTransform, CharacterCombatAgent combatAgent, NpcAutoSetupOptions options, SetupReport report)
        {
            if (weaponTransform == null || npcRoot == null || combatAgent == null)
            {
                return;
            }

            var weapon = weaponTransform.GetComponent<MeleeWeapon>();
            if (weapon == null)
            {
                weapon = Undo.AddComponent<MeleeWeapon>(weaponTransform.gameObject);
                report.AddAction($"Added MeleeWeapon to '{weaponTransform.name}'.");
            }

            var weaponSO = new SerializedObject(weapon);
            weaponSO.FindProperty("owner").objectReferenceValue = combatAgent;
            weaponSO.FindProperty("ownerTransform").objectReferenceValue = combatAgent.transform;
            weaponSO.FindProperty("attackOrigin").objectReferenceValue = weaponTransform;
            weaponSO.FindProperty("attackRadius").floatValue = options.meleeRadius;
            weaponSO.FindProperty("attackAngle").floatValue = options.meleeAngle;
            weaponSO.FindProperty("damageMultiplier").floatValue = options.meleeDamageMultiplier;
            weaponSO.FindProperty("hitMask").intValue = options.meleeHitMask.value;
            weaponSO.FindProperty("includeTriggers").boolValue = options.meleeIncludeTriggers;
            weaponSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(weapon);
            weapon.SetOwner(combatAgent);

            var animator = npcRoot.GetComponentInChildren<Animator>();
            ConfigureAttackWindowDriver(
                npcRoot,
                animator,
                combatAgent,
                weapon,
                options.attackStateName,
                options.attackLayer,
                options.attackWindowStart,
                options.attackWindowEnd,
                report);
        }

        static void ConfigureNpcHealthBar(GameObject npcRoot, CharacterCombatAgent combatAgent, NpcAutoSetupOptions options, SetupReport report)
        {
            if (npcRoot == null)
            {
                return;
            }

            var bar = npcRoot.GetComponent<NpcWorldspaceHealthBar>();
            if (!options.attachHealthBar)
            {
                if (bar != null)
                {
                    Undo.DestroyObjectImmediate(bar);
                    report.AddAction("Removed NpcWorldspaceHealthBar.");
                }
                return;
            }

            if (bar == null)
            {
                bar = Undo.AddComponent<NpcWorldspaceHealthBar>(npcRoot);
                report.AddAction("Added NpcWorldspaceHealthBar.");
            }

            var barSO = new SerializedObject(bar);
            barSO.FindProperty("combatAgent").objectReferenceValue = combatAgent;
            barSO.FindProperty("anchor").objectReferenceValue = npcRoot.transform;
            barSO.FindProperty("worldOffset").vector3Value = options.healthBarOffset;
            barSO.FindProperty("barSize").vector2Value = options.healthBarSize;
            barSO.FindProperty("uniformScale").floatValue = Mathf.Max(0.01f, options.healthBarScale);
            barSO.FindProperty("alwaysVisible").boolValue = options.healthBarAlwaysVisible;
            barSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(bar);
        }

        static FloatingCombatTextSpawner ConfigureFloatingCombatTextSpawner(GameObject root, bool attach, Vector3 offset, Color color, SetupReport report)
        {
            if (root == null)
            {
                return null;
            }

            var spawner = root.GetComponent<FloatingCombatTextSpawner>();
            if (!attach)
            {
                if (spawner != null)
                {
                    Undo.DestroyObjectImmediate(spawner);
                    report.AddAction("Removed FloatingCombatTextSpawner.");
                }
                return null;
            }

            if (spawner == null)
            {
                spawner = Undo.AddComponent<FloatingCombatTextSpawner>(root);
                report.AddAction("Added FloatingCombatTextSpawner.");
            }

            var spawnerSO = new SerializedObject(spawner);
            spawnerSO.FindProperty("anchor").objectReferenceValue = root.transform;
            spawnerSO.FindProperty("spawnOffset").vector3Value = offset;
            spawnerSO.FindProperty("lightDamageColor").colorValue = color;
            spawnerSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(spawner);
            return spawner;
        }

        static void ConfigureCapsule(CapsuleCollider capsule, GameObject root, SetupReport report)
        {
            Undo.RecordObject(capsule, "Configure Capsule Collider");
            capsule.direction = 1;
            Bounds combinedBounds;
            if (TryGetRendererBounds(root, out combinedBounds))
            {
                Transform rootTransform = root.transform;
                Vector3 lossy = rootTransform.lossyScale;
                float scaleX = Mathf.Max(Mathf.Abs(lossy.x), 0.0001f);
                float scaleY = Mathf.Max(Mathf.Abs(lossy.y), 0.0001f);
                float scaleZ = Mathf.Max(Mathf.Abs(lossy.z), 0.0001f);

                Vector3 localCenter = rootTransform.InverseTransformPoint(combinedBounds.center);
                Vector3 worldBottom = new Vector3(combinedBounds.center.x, combinedBounds.min.y, combinedBounds.center.z);
                float localBottomY = rootTransform.InverseTransformPoint(worldBottom).y;

                float localRadiusX = combinedBounds.extents.x / scaleX;
                float localRadiusZ = combinedBounds.extents.z / scaleZ;
                float radius = Mathf.Clamp(Mathf.Max(localRadiusX, localRadiusZ), 0.2f, 1f);
                float localHeight = combinedBounds.size.y / scaleY;
                float minHeight = Mathf.Max(radius * 2f + 0.01f, 1.4f);
                float height = Mathf.Max(localHeight, minHeight);

                capsule.radius = radius;
                capsule.height = height;
                float centerY = localBottomY + height * 0.5f + 0.05f;
                capsule.center = new Vector3(localCenter.x, centerY, localCenter.z);
                report.AddAction("Aligned capsule to mesh bounds.");
            }
            else
            {
                capsule.center = new Vector3(0f, 0.7f, 0f);
                capsule.height = 1.8f;
                capsule.radius = 0.2f;
                report.AddWarning("Could not find any renderers to size the capsule; used default human proportions.");
            }
            EditorUtility.SetDirty(capsule);
        }

        static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            bounds = default;
            bool hasBounds = false;
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        static Transform FindTransformByKeywords(GameObject root, string[] keywords)
        {
            if (root == null || keywords == null || keywords.Length == 0)
            {
                return null;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var keyword in keywords)
            {
                foreach (var t in transforms)
                {
                    if (t == root.transform)
                    {
                        continue;
                    }

                    if (t.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return t;
                    }
                }
            }

            return null;
        }

        public static bool HeroAnimatorHasAllRequiredClips()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(HeroAnimatorPath);
            if (controller == null)
            {
                return true;
            }

            foreach (var layer in controller.layers)
            {
                if (!StateMachineHasClips(layer.stateMachine))
                {
                    return false;
                }
            }

            return true;
        }

        static bool StateMachineHasClips(AnimatorStateMachine stateMachine)
        {
            foreach (var child in stateMachine.states)
            {
                if (!MotionTreeHasClips(child.state.motion))
                {
                    return false;
                }
            }

            foreach (var sub in stateMachine.stateMachines)
            {
                if (!StateMachineHasClips(sub.stateMachine))
                {
                    return false;
                }
            }

            return true;
        }

        static bool MotionTreeHasClips(Motion motion)
        {
            if (motion == null)
            {
                return false;
            }

            var tree = motion as BlendTree;
            if (tree == null)
            {
                return true;
            }

            foreach (var child in tree.children)
            {
                if (!MotionTreeHasClips(child.motion))
                {
                    return false;
                }
            }

            return true;
        }

        static void AssignObject(SerializedObject obj, string propertyPath, UnityEngine.Object value, SetupReport report, string label)
        {
            if (value == null)
            {
                report.AddWarning($"Missing reference for {label}. Assign it manually if required.");
                return;
            }

            var property = obj.FindProperty(propertyPath);
            if (property == null)
            {
                report.AddWarning($"Could not find serialized property '{propertyPath}' when assigning {label}.");
                return;
            }

            property.objectReferenceValue = value;
        }

        static void SetFloat(SerializedObject obj, string propertyPath, float value)
        {
            var property = obj.FindProperty(propertyPath);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        static void SetInt(SerializedObject obj, string propertyPath, int value)
        {
            var property = obj.FindProperty(propertyPath);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        static void SetString(SerializedObject obj, string propertyPath, string value)
        {
            var property = obj.FindProperty(propertyPath);
            if (property != null)
            {
                property.stringValue = value ?? string.Empty;
            }
        }

        static void SetBool(SerializedObject obj, string propertyPath, bool value)
        {
            var property = obj.FindProperty(propertyPath);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        static void SetVector3(SerializedObject obj, string propertyPath, Vector3 value)
        {
            var property = obj.FindProperty(propertyPath);
            if (property != null)
            {
                property.vector3Value = value;
            }
        }

        static void SetVector2(SerializedObject obj, string propertyPath, Vector2 value)
        {
            var property = obj.FindProperty(propertyPath);
            if (property != null)
            {
                property.vector2Value = value;
            }
        }

        static void SetColor(SerializedObject obj, string propertyPath, Color value)
        {
            var property = obj.FindProperty(propertyPath);
            if (property != null)
            {
                property.colorValue = value;
            }
        }

        static bool EnsureTagExists(string tag, SetupReport report)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return false;
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                report.AddWarning("Cannot load TagManager.asset to add missing tags.");
                return false;
            }

            var tagManager = assets[0];
            var serializedObject = new SerializedObject(tagManager);
            var tagsProp = serializedObject.FindProperty("tags");
            if (tagsProp == null)
            {
                report.AddWarning("TagManager asset missing 'tags' property.");
                return false;
            }

            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                var element = tagsProp.GetArrayElementAtIndex(i);
                if (element != null && element.stringValue == tag)
                {
                    return true;
                }
            }

            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            var newElement = tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1);
            newElement.stringValue = tag;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            report.AddAction($"Added missing tag '{tag}'.");
            return true;
        }

        static void SetRigHierarchyHidden(GameObject root, bool hidden, SetupReport report)
        {
            if (root == null)
            {
                return;
            }

            var skinnedMeshes = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinnedMeshes == null || skinnedMeshes.Length == 0)
            {
                if (hidden)
                {
                    report.AddWarning("No skinned meshes found to hide rig bones.");
                }
                else
                {
                    report.AddAction("No skinned meshes present; nothing to unhide.");
                }
                return;
            }

            var visited = new HashSet<Transform>();
            foreach (var skinned in skinnedMeshes)
            {
                if (skinned == null)
                {
                    continue;
                }

                if (skinned.rootBone != null)
                {
                    ApplyHideFlagsRecursive(skinned.rootBone, hidden, visited);
                }

                var bones = skinned.bones;
                if (bones == null)
                {
                    continue;
                }

                foreach (var bone in bones)
                {
                    if (bone == null)
                    {
                        continue;
                    }
                    ApplyHideFlagsRecursive(bone, hidden, visited);
                }
            }

            EditorApplication.RepaintHierarchyWindow();
            report.AddAction(hidden ? "Hid rig bone hierarchy in the Hierarchy window." : "Revealed rig bone hierarchy.");
        }

        static void ApplyHideFlagsRecursive(Transform bone, bool hidden, HashSet<Transform> visited)
        {
            if (bone == null)
            {
                return;
            }

            if (!visited.Add(bone))
            {
                return;
            }

            var go = bone.gameObject;
            var flags = go.hideFlags;
            if (hidden)
            {
                if ((flags & HideFlags.HideInHierarchy) == 0)
                {
                    go.hideFlags = flags | HideFlags.HideInHierarchy;
                    EditorUtility.SetDirty(go);
                }
            }
            else
            {
                if ((flags & HideFlags.HideInHierarchy) != 0)
                {
                    go.hideFlags = flags & ~HideFlags.HideInHierarchy;
                    EditorUtility.SetDirty(go);
                }
            }

            for (int i = 0; i < bone.childCount; i++)
            {
                ApplyHideFlagsRecursive(bone.GetChild(i), hidden, visited);
            }
        }

        static void RemoveChildCameras(GameObject heroRoot, SetupReport report)
        {
            var cameras = heroRoot.GetComponentsInChildren<Camera>(true);
            if (cameras == null || cameras.Length == 0)
            {
                return;
            }

            foreach (var camera in cameras)
            {
                var go = camera.gameObject;
                Undo.DestroyObjectImmediate(camera);
                var listener = go.GetComponent<AudioListener>();
                if (listener != null)
                {
                    Undo.DestroyObjectImmediate(listener);
                }

                if (go.transform.childCount == 0 && go.GetComponents<Component>().Length == 1)
                {
                    Undo.DestroyObjectImmediate(go);
                }
                report.AddAction($"Removed existing camera '{go.name}'.");
            }
        }
    }
}
#else
using UnityEditor;

namespace HeroCharacter.Editor
{
    public class HeroCombatAutoWiringWindow : EditorWindow
    {
        const string MenuPath = "Hero Character/Combat Auto Wiring";

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<HeroCombatAutoWiringWindow>("Hero Combat Auto Wiring");
            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Enable Unity's Input System (Project Settings → Player → Active Input Handling) to use the Hero Combat Auto Wiring tool.", MessageType.Warning);
        }
    }
}
#endif
