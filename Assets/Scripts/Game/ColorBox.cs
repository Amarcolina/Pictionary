using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

public class ColorBox : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
  private const float SCROLL_SENSITIVITY = -0.03f;

  [SerializeField]
  [FormerlySerializedAs("brush")]
  private Paintbrush _brush;

  [SerializeField]
  [FormerlySerializedAs("canAdjustColor")]
  private bool _canAdjustColor = true;

  private Image _image;
  private bool _isHovered;

  private float _hue;
  private float _saturation;
  private float _value;

  private float _center;

  private void Start() {
    _image = GetComponent<Image>();

    Color.RGBToHSV(_image.color, out _hue, out _saturation, out _value);
    _center = 0.5f;

    GetComponent<Button>().onClick.AddListener(onClick);
  }

  private void Update() {
    if (_isHovered && _canAdjustColor) {
      _center = Mathf.Clamp01(_center + Input.mouseScrollDelta.y * SCROLL_SENSITIVITY);

      if (_center > 0.5f) {
        float percent = Mathf.InverseLerp(0.5f, 1, _center);
        _image.color = Color.HSVToRGB(_hue, Mathf.Lerp(_saturation, 0, percent), Mathf.Lerp(_value, 1, percent));
      } else {
        float percent = Mathf.InverseLerp(0, 0.5f, _center);
        _image.color = Color.HSVToRGB(_hue, Mathf.Lerp(1, _saturation, percent), Mathf.Lerp(0, _value, percent));
      }

    }
  }

  private void onClick() {
    _brush.SetColor(_image.color);
  }

  public void OnPointerEnter(PointerEventData eventData) {
    _isHovered = true;
  }

  public void OnPointerExit(PointerEventData eventData) {
    _isHovered = false;
  }
}
