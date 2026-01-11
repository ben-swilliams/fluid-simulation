using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SliderLabel : MonoBehaviour
{
    private Slider slider;
    private TMP_Text label;

    void Awake()
    {
        slider = GetComponentInChildren<Slider>();

        label = GetComponentInChildren<TMP_Text>(true);
    }

    void OnEnable()
    {
        UpdateLabel(slider.value);
        slider.onValueChanged.AddListener(UpdateLabel);
    }

    void OnDisable()
    {
        slider.onValueChanged.RemoveListener(UpdateLabel);
    }

    void UpdateLabel(float value)
    {
        label.text = value.ToString();
    }
}
