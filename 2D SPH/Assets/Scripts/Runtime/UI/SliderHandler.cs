using UnityEngine;
using UnityEngine.UI;

public class SliderHandler : MonoBehaviour
{
    [SerializeField] Tweakable targetScript;
    [SerializeField] string targetProperty;
    
    Slider sourceSlider;

    void Start()
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
}
