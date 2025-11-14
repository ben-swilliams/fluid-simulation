  using UnityEngine;
  using UnityEngine.UI;
  using UnityEngine.Events;

  public class DropdownBinding : MonoBehaviour
  {
      [SerializeField] UnityEvent<int> onValueChanged;

      void Start()
      {
        Dropdown dropdown = GetComponent<Dropdown>();
        dropdown.onValueChanged.AddListener(value => onValueChanged.Invoke(value));
      }
  }