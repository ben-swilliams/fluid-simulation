using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

public abstract class Tweakable : MonoBehaviour
{
    Dictionary<string, FieldInfo> settings;

    void Awake()
    {
        BuildCache();
    }

    void OnValidate()
    {
        BuildCache();
    }
    void BuildCache()
    {
        settings = new Dictionary<string, FieldInfo>();

        var type = GetType();
        foreach (var f in type.GetFields(
                     BindingFlags.Instance |
                     BindingFlags.NonPublic |
                     BindingFlags.Public))
        {
            if (f.IsDefined(typeof(SerializeField), true) &&
                f.FieldType == typeof(float)) // only expose floats
            {
                settings[f.Name] = f;
            }
        }
    }

    public abstract void UpdateSettings();

    public List<string> GetTweakableNames()
    {
        if (settings == null)
            BuildCache();

        return new List<string>(settings.Keys);
    }

    public void Set(string name, float value)
    {
        if (settings.TryGetValue(name, out var f))
            f.SetValue(this, value);
    }
}