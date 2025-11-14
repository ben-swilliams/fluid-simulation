  using UnityEngine;
  using UnityEngine.UI;
  using UnityEngine.Events;

  public class SliderBinding : MonoBehaviour
  {
      [SerializeField] UnityEvent<float> onValueChanged;

      void Start()
      {
        Slider slider = GetComponent<Slider>();
        slider.onValueChanged.AddListener(value => onValueChanged.Invoke(value));
      }
  }