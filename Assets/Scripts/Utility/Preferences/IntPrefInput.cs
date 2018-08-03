using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

[RequireComponent(typeof(InputField))]
public class IntPrefInput : MonoBehaviour {

  [SerializeField]
  [FormerlySerializedAs("preference")]
  private IntPref _preference;

  private InputField _field;

  private void Awake() {
    _field = GetComponent<InputField>();
  }

  private void OnEnable() {
    _field.text = _preference.value.ToString();
    _field.characterValidation = InputField.CharacterValidation.Integer;
    _field.onEndEdit.AddListener(onTextChange);
  }

  private void OnDisable() {
    _field.onEndEdit.RemoveListener(onTextChange);
  }

  private void onTextChange(string text) {
    int value;
    if (!int.TryParse(text, out value)) {
      _field.text = _preference.value.ToString();
      return;
    }

    _preference.value = value;
  }
}
