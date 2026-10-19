
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Transform))]
[CanEditMultipleObjects]
public class PositionSnap : Editor
{
    // Cached to avoid allocating a new GUIContent on every Inspector repaint.
    private static readonly GUIContent snapIntegerLabel = new GUIContent("对齐到整数");
    private static readonly GUIContent snapHalfLabel = new GUIContent("对齐到 0.5");

    // Horizontal gap between the two snap buttons, in pixels.
    private const float SnapButtonSpacing = 6f;

    private static GUIStyle snapButtonStyle;
    private static GUIStyle snapButtonStyleSource;

    private SerializedProperty positionProperty;
    private SerializedProperty rotationProperty;
    private SerializedProperty scaleProperty;

    private void OnEnable()
    {
        positionProperty = serializedObject.FindProperty("m_LocalPosition");
        rotationProperty = serializedObject.FindProperty("m_LocalRotation");
        scaleProperty = serializedObject.FindProperty("m_LocalScale");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Position
        EditorGUILayout.PropertyField(
            positionProperty,
            new GUIContent("Position")
        );

        // Position snapping buttons
        // A leading FlexibleSpace pushes the button pair to the right edge.
        EditorGUILayout.BeginHorizontal();

        GUILayout.FlexibleSpace();

        if (SnapButton(snapIntegerLabel))
        {
            SnapPosition(1f);
        }

        GUILayout.Space(SnapButtonSpacing);

        if (SnapButton(snapHalfLabel))
        {
            SnapPosition(0.5f);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(3);

        // Rotation: display Euler angles instead of quaternion values.
        EditorGUI.showMixedValue =
            rotationProperty.hasMultipleDifferentValues;

        Quaternion rotation = rotationProperty.quaternionValue;

        EditorGUI.BeginChangeCheck();

        Vector3 eulerAngles = EditorGUILayout.Vector3Field(
            "Rotation",
            rotation.eulerAngles
        );

        if (EditorGUI.EndChangeCheck())
        {
            rotationProperty.quaternionValue =
                Quaternion.Euler(eulerAngles);
        }

        EditorGUI.showMixedValue = false;

        // Scale
        EditorGUILayout.PropertyField(
            scaleProperty,
            new GUIContent("Scale")
        );

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Draws a button whose width follows its own label.
    /// GUILayout.Button stretches to fill the row by default, so pass
    /// ExpandWidth(false) to pin the width to the label's measured size.
    /// </summary>
    private static bool SnapButton(GUIContent content)
    {
        return GUILayout.Button(
            content,
            GetSnapButtonStyle(),
            GUILayout.ExpandWidth(false)
        );
    }

    /// <summary>
    /// Returns a copy of the current button style with its horizontal margin
    /// stripped, so the gap between the two snap buttons is exactly
    /// SnapButtonSpacing instead of spacing + margin + spacing.
    /// Rebuilt whenever the skin changes (theme switch, DPI change).
    /// </summary>
    private static GUIStyle GetSnapButtonStyle()
    {
        GUIStyle skinButton = GUI.skin.button;

        if (snapButtonStyle == null || snapButtonStyleSource != skinButton)
        {
            snapButtonStyleSource = skinButton;
            snapButtonStyle = new GUIStyle(skinButton);

            RectOffset margin = snapButtonStyle.margin;

            snapButtonStyle.margin = new RectOffset(
                0,
                0,
                margin.top,
                margin.bottom
            );
        }

        return snapButtonStyle;
    }

    private void SnapPosition(float step)
    {
        // Apply pending Inspector edits before changing positions.
        serializedObject.ApplyModifiedProperties();

        // Record all selected objects for Undo.
        Undo.RecordObjects(
            targets,
            step == 1f ? "Snap Position to Integer"
                       : "Snap Position to 0.5"
        );

        foreach (Object obj in targets)
        {
            Transform t = obj as Transform;

            if (t == null)
                continue;

            Vector3 p = t.localPosition;

            p.x = Mathf.Round(p.x / step) * step;
            p.y = Mathf.Round(p.y / step) * step;
            p.z = Mathf.Round(p.z / step) * step;

            t.localPosition = p;

            // Notify Unity that this object has changed.
            EditorUtility.SetDirty(t);
        }

        // Refresh Inspector values after snapping.
        serializedObject.Update();
        Repaint();
    }
}
