using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIHandler : MonoBehaviour
{
    [SerializeField] Button[] tabButtons;
    [SerializeField] GameObject[] settings;

    int selectedIndex = 0;

    void Start()
    {
        EventSystem.current.SetSelectedGameObject(tabButtons[0].gameObject);
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

        settings[selectedIndex].SetActive(false);
        selectedIndex = index;
        settings[selectedIndex].SetActive(true);
    }
}
