using UnityEngine;
using UnityEngine.UI;

public class UIHandler : MonoBehaviour
{
    [SerializeField] Simulate sim;
    [SerializeField] Button[] tabButtons;
    [SerializeField] Slider simSpeedSlider;

    int selectedIndex = 0;

    void Start()
    {
        RegisterListeners();
    }

    void RegisterListeners()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            tabButtons[i].onClick.AddListener(() =>
            {
                SelectTab(i);
            });
        }

        simSpeedSlider.onValueChanged.AddListener((float value) =>
        {
            sim.SimulationSpeed = value;
        });
    }

    void SelectTab(int index)
    {
        if (selectedIndex == index) return;

        selectedIndex = index;
    }
}
