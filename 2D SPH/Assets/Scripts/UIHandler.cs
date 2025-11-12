using UnityEngine;
using UnityEngine.UI;

public class UIHandler : MonoBehaviour
{
    [SerializeField] Button[] tabButtons;

    private int selectedIndex = 0;

    void Start()
    {
        RegisterListeners();
    }

    void Update()
    {
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
    }

    void SelectTab(int index)
    {
        if (selectedIndex == index) return;

        selectedIndex = index;
    }
}
