using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class Tweakable : MonoBehaviour
{
    Dictionary<string, FieldInfo> settings;

    void Awake()
    {
        settings = new Dictionary<string, FieldInfo>();

        var type = GetType();
        foreach (var f in type.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public))
        {
            if (f.IsDefined(typeof(SerializeField), true))
            {
                settings[f.Name] = f;
            }
        }
    }

    public void ChangeSetting(string targetProperty, float newValue)
    {
        if (!settings.TryGetValue(targetProperty, out var f)) return;

        f.SetValue(this, newValue);
    }
}