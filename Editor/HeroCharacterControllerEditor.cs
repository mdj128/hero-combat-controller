using UnityEditor;
using UnityEngine;

namespace HeroCharacter.Editor
{
    [CustomEditor(typeof(HeroCharacterController))]
    public class HeroCharacterControllerEditor : UnityEditor.Editor
    {
        SerializedProperty cameraProp;
        SerializedProperty movementProp;
        SerializedProperty footstepsProp;
        SerializedProperty interactionProp;
        SerializedProperty crosshairProp;
        SerializedProperty animationProp;
        SerializedProperty inputProp;
        SerializedProperty eventsProp;
        SerializedProperty debugProp;

        bool showCamera = true;
        bool showMovement = true;
        bool showFootsteps = false;
        bool showInteraction = false;
        bool showCrosshair = true;
        bool showAnimation = true;
        bool showInput = true;
        bool showEvents = false;
        bool showDebug = false;

        void OnEnable()
        {
            CacheProperties();
        }

        public override void OnInspectorGUI()
        {
            if (cameraProp == null)
            {
                CacheProperties();
            }

            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space();

            DrawSection(ref showCamera, "Camera", cameraProp);
            DrawSection(ref showMovement, "Movement", movementProp);
            DrawSection(ref showFootsteps, "Footsteps", footstepsProp);
            DrawSection(ref showInteraction, "Interaction", interactionProp);
            DrawSection(ref showCrosshair, "Crosshair", crosshairProp);
            DrawSection(ref showAnimation, "Animation", animationProp);
            DrawSection(ref showInput, "Input", inputProp);
            DrawSection(ref showEvents, "Events", eventsProp);
            DrawSection(ref showDebug, "Debug", debugProp);

            serializedObject.ApplyModifiedProperties();
        }

        new void DrawHeader()
        {
            EditorGUILayout.LabelField("Hero Character Controller", Styles.Header);

            var controller = (HeroCharacterController)target;
            if (controller != null && controller.enabled && controller.gameObject.activeInHierarchy)
            {
                if (controller.GetComponentInChildren<Camera>() == null)
                {
                    EditorGUILayout.HelpBox("Assign a Camera reference in Camera → Player Camera.", MessageType.Warning);
                }
            }
        }

        void DrawSection(ref bool foldout, string title, SerializedProperty property)
        {
            if (property == null)
            {
                return;
            }

            EditorGUILayout.Space(2f);
            var rect = EditorGUILayout.GetControlRect();
            foldout = EditorGUI.Foldout(rect, foldout, title, true, Styles.SectionFoldout);
            if (!foldout)
            {
                return;
            }

            EditorGUI.indentLevel++;
            if (property == inputProp)
            {
                EditorGUILayout.HelpBox("Auto bind searches the attached PlayerInput for actions using the names listed below (Move, Look, Zoom, etc.). Assign a PlayerInput or uncheck Auto Bind to reference one manually.", MessageType.Info);
            }
            if (property.propertyType == SerializedPropertyType.Generic && property.hasVisibleChildren)
            {
                DrawChildProperties(property);
            }
            else
            {
                EditorGUILayout.PropertyField(property, GUIContent.none, true);
            }
            EditorGUI.indentLevel--;
        }

        void DrawChildProperties(SerializedProperty parent)
        {
            var iterator = parent.Copy();
            var endProperty = iterator.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                EditorGUILayout.PropertyField(iterator, true);
                enterChildren = false;
            }
        }

        void CacheProperties()
        {
            cameraProp = serializedObject.FindProperty("cameraSettings");
            movementProp = serializedObject.FindProperty("movement");
            footstepsProp = serializedObject.FindProperty("footsteps");
            interactionProp = serializedObject.FindProperty("interaction");
            crosshairProp = serializedObject.FindProperty("crosshair");
            animationProp = serializedObject.FindProperty("animationSettings");
            inputProp = serializedObject.FindProperty("input");
            eventsProp = serializedObject.FindProperty("events");
            debugProp = serializedObject.FindProperty("debug");
        }

        static class Styles
        {
            public static readonly GUIStyle Header;
            public static readonly GUIStyle Subheader;
            public static readonly GUIStyle SectionFoldout;

            static Styles()
            {
                Header = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 15,
                    alignment = TextAnchor.MiddleCenter
                };

                Subheader = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Italic
                };

                SectionFoldout = new GUIStyle(EditorStyles.foldout)
                {
                    fontStyle = FontStyle.Bold,
                    margin = new RectOffset(4, 0, 2, 2)
                };
            }
        }
    }
}
