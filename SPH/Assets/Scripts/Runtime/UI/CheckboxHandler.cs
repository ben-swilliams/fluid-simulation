using UnityEngine;
using UnityEngine.UI;

public class CheckboxHandler : MonoBehaviour
{
    [SerializeField] Tweakable targetScript;
    [SerializeField] string targetProperty;
    
    Toggle sourceCheckbox;

    void Start()
    {
        sourceCheckbox = GetComponent<Toggle>();
    }
    void Update()
    {
        sourceCheckbox.isOn = targetScript.Get(targetProperty) > 0;
    }

    public void ChangeSetting(bool x)
    {
       targetScript.Set(targetProperty, x ? 1f : 0f); 
    }
}
