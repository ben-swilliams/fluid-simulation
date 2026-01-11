using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SliderHandler : MonoBehaviour
{
    [SerializeField] Tweakable targetScript;
    [SerializeField] string targetProperty;
    [SerializeField] TMP_Text label;
    
    Slider sourceSlider;

    void Awake()
    {
        sourceSlider = GetComponent<Slider>();
    }

    void Update()
    {
        sourceSlider.value = targetScript.Get(targetProperty);
    }

    public void ChangeSetting(float x)
    {
       targetScript.Set(targetProperty, x); 
    }

    void OnEnable()
    {
        UpdateLabel(sourceSlider.value);
        sourceSlider.onValueChanged.AddListener(UpdateLabel);
    }

    void OnDisable()
    {
        sourceSlider.onValueChanged.RemoveListener(UpdateLabel);
    }

    void UpdateLabel(float value)
    {
        if (!label) return;
        label.text = value.ToString();
    }
}
