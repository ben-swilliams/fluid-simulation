using UnityEngine;
using TMPro;
using System;
using System.Reflection;

public class DropdownHandler : MonoBehaviour
{
    [SerializeField] Tweakable targetScript;
    [SerializeField] string targetProperty;
    [SerializeField] bool autoPopulate = true;

    TMP_Dropdown sourceDropdown;

    void Start()
    {
        sourceDropdown = GetComponent<TMP_Dropdown>();

        if (autoPopulate && targetScript != null && !string.IsNullOrEmpty(targetProperty))
        {
            PopulateOptionsFromEnum();
        }
    }

    void PopulateOptionsFromEnum()
    {
        var fieldInfo = targetScript.GetType().GetField(targetProperty,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        if (fieldInfo != null && fieldInfo.FieldType.IsEnum && sourceDropdown != null)
        {
            sourceDropdown.options.Clear();
            var enumNames = Enum.GetNames(fieldInfo.FieldType);

            foreach (var enumName in enumNames)
            {
                sourceDropdown.options.Add(new TMP_Dropdown.OptionData(enumName));
            }

            sourceDropdown.value = Mathf.FloorToInt(targetScript.Get(targetProperty));
            sourceDropdown.RefreshShownValue();
        }
    }

    void Update()
    {
        if (targetScript == null || sourceDropdown == null) return;
        sourceDropdown.value = Mathf.FloorToInt(targetScript.Get(targetProperty));
    }

    public void ChangeSetting(int x)
    {
        if (targetScript != null)
            targetScript.Set(targetProperty, x);
    }
}
