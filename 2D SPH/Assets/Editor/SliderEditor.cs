using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Slider))]
public class SliderEditor : Editor
{
    SerializedProperty targetScriptProp;
    SerializedProperty targetPropertyProp;

    void OnEnable()
    {
        targetScriptProp = serializedObject.FindProperty("targetScript");
        targetPropertyProp = serializedObject.FindProperty("targetProperty");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(targetScriptProp);

        var target = targetScriptProp.objectReferenceValue as Tweakable;

        if (target != null)
        {
            var names = target.GetTweakableNames();
            int index = Mathf.Max(0, names.IndexOf(targetPropertyProp.stringValue));

            index = EditorGUILayout.Popup("Target Property", index, names.ToArray());

            if (index >= 0 && index < names.Count)
                targetPropertyProp.stringValue = names[index];
        }
        else
        {
            EditorGUILayout.PropertyField(targetPropertyProp);
        }

        serializedObject.ApplyModifiedProperties();
    }
}