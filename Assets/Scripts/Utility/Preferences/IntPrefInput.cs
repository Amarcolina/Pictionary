using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(InputField))]
public class IntPrefInput : MonoBehaviour {

  public IntPref preference;

  private InputField _field;

  private void Awake() {
    _field = GetComponent<InputField>();
  }

  private void OnEnable() {
    _field.text = preference.value.ToString();
    _field.characterValidation = InputField.CharacterValidation.Integer;
    _field.onEndEdit.AddListener(onTextChange);
  }

  private void OnDisable() {
    _field.onEndEdit.RemoveListener(onTextChange);
  }

  private void onTextChange(string text) {
    int value;
    if (!int.TryParse(text, out value)) {
      _field.text = preference.value.ToString();
      return;
    }

    preference.value = value;
  }
}
