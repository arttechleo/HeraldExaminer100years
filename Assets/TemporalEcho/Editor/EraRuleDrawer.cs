#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TemporalEcho;

[CustomPropertyDrawer(typeof(EraRule))]
public class EraRuleDrawer : PropertyDrawer
{
    private const float LineHeight = 18f;
    private const float Spacing = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return 5 * (LineHeight + Spacing) + Spacing;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float y = position.y + Spacing;
        float w = position.width;

        var subjectProp = property.FindPropertyRelative("subject");
        var prefabProp = property.FindPropertyRelative("prefab");
        var posProp = property.FindPropertyRelative("localPosition");
        var rotProp = property.FindPropertyRelative("localEulerRotation");
        var scaleProp = property.FindPropertyRelative("localScale");

        Rect r = new Rect(position.x, y, w, LineHeight);
        EditorGUI.PropertyField(r, subjectProp);
        y += LineHeight + Spacing;

        r = new Rect(position.x, y, w, LineHeight);
        // Draw prefab as explicit GameObject so drag-drop and picker don't show "type mismatch"
        GUIContent prefabLabel = new GUIContent("Prefab");
        prefabProp.objectReferenceValue = EditorGUI.ObjectField(r, prefabLabel, prefabProp.objectReferenceValue, typeof(GameObject), false) as GameObject;
        y += LineHeight + Spacing;

        r = new Rect(position.x, y, w, LineHeight);
        EditorGUI.PropertyField(r, posProp);
        y += LineHeight + Spacing;

        r = new Rect(position.x, y, w, LineHeight);
        EditorGUI.PropertyField(r, rotProp);
        y += LineHeight + Spacing;

        r = new Rect(position.x, y, w, LineHeight);
        EditorGUI.PropertyField(r, scaleProp);

        EditorGUI.EndProperty();
    }
}
#endif
