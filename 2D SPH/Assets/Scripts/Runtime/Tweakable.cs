using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using System;

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
            if (f.IsDefined(typeof(SerializeField), true))
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
        {
            if (f.FieldType == typeof(int))
                f.SetValue(this, Mathf.FloorToInt(value));
            else
                f.SetValue(this, value);
            UpdateSettings();
        }
    }

    public float Get(string name)
    {
        if (settings.TryGetValue(name, out var f))
        {
            return float.Parse(f.GetValue(this).ToString());
        }

        return 0f;
    }
}