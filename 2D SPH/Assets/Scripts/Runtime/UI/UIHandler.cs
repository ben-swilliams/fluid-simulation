using UnityEngine;
using UnityEngine.UI;

public class UIHandler : MonoBehaviour
{
    [SerializeField] Color selectedColour;
    [SerializeField] Button[] tabButtons;
    [SerializeField] GameObject[] settings;

    int selectedIndex = 0;

    void Start()
    {
        for (int i = 0; i < tabButtons.Length; i++) settings[i].SetActive(false);
        tabButtons[0].GetComponent<Image>().color = selectedColour;
        SelectTab(0);
        RegisterListeners();
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
}
