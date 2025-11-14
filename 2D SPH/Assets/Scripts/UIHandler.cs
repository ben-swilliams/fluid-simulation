using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D.IK;
using UnityEngine.UI;

public class UIHandler : MonoBehaviour
{
    [SerializeField] Color selectedColour;
    [SerializeField] Button[] tabButtons;
    [SerializeField] GameObject[] settings;
    [SerializeField] GameObject WCSPHSettings;
    [SerializeField] GameObject IISPHSettings;

    [SerializeField] GameObject VelocitySettings;
    [SerializeField] GameObject DensitySettings;

    int selectedIndex = 0;

    void Start()
    {
        tabButtons[0].GetComponent<Image>().color = selectedColour;
        SelectTab(0);
        RegisterListeners();

        WCSPHSettings.SetActive(true);
        IISPHSettings.SetActive(false);
    }

    void RegisterListeners()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int index = i;
            tabButtons[i].onClick.AddListener(() =>
            {
                SelectTab(index);
            });
        }
    }

    void SelectTab(int index)
    {
        if (selectedIndex == index) return;
        ColourButton(index);

        settings[selectedIndex].SetActive(false);
        selectedIndex = index;
        settings[selectedIndex].SetActive(true);
    }

    void ColourButton(int index)
    {
        tabButtons[index].GetComponent<Image>().color = selectedColour;
        tabButtons[selectedIndex].GetComponent<Image>().color = Color.white;
    }

    public void SelectSolverSettings(int solver)
    {
        if (selectedIndex != 2) return;

        if (solver == 0)
        {
            WCSPHSettings.SetActive(true);
            IISPHSettings.SetActive(false);
        }
        if (solver == 1)
        {
            IISPHSettings.SetActive(true);
            WCSPHSettings.SetActive(false);
        }
    }

    public void SelectPropertySettings(int property)
    {
        if (selectedIndex != 3) return;

        if (property == 0)
        {
            VelocitySettings.SetActive(true);
            DensitySettings.SetActive(false);
        }
        if (property == 1)
        {
            VelocitySettings.SetActive(false);
            DensitySettings.SetActive(true);
        }
    }
}
