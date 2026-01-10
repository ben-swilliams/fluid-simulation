using UnityEngine;

public class Slider : MonoBehaviour
{
    [SerializeField] Tweakable targetScript;
    [SerializeField] string targetProperty;
    
    public void ChangeSetting(float x)
    {
       targetScript.ChangeSetting(targetProperty, x); 
    }
}
