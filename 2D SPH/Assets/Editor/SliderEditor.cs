using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SliderHandler))]
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

        if (targetScriptProp.objectReferenceValue != null)
        {
            var component = targetScriptProp.objectReferenceValue as Component;
            if (component != null)
            {
                var allTweakables = component.GetComponents<Tweakable>();
                if (allTweakables.Length > 1)
                {
                    var labels = new string[allTweakables.Length];
                    int currentIndex = 0;

                    for (int i = 0; i < allTweakables.Length; i++)
                    {
                        labels[i] = allTweakables[i].GetType().Name;
                        if (allTweakables[i] == targetScriptProp.objectReferenceValue)
                            currentIndex = i;
                    }

                    int newIndex = EditorGUILayout.Popup("Component", currentIndex, labels);
                    if (newIndex != currentIndex)
                    {
                        targetScriptProp.objectReferenceValue = allTweakables[newIndex];
                    }
                }
            }
        }

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