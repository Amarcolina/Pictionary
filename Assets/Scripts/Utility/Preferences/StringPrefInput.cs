using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(InputField))]
public class StringPrefInput : MonoBehaviour {

  public StringPref preference;

  private InputField _field;

  private void Awake() {
    _field = GetComponent<InputField>();
  }

  private void OnEnable() {
    _field.text = preference.value.ToString();
    _field.onEndEdit.AddListener(onTextChange);
  }

  private void OnDisable() {
    _field.onEndEdit.RemoveListener(onTextChange);
  }

  private void onTextChange(string text) {
    preference.value = text;
  }
}
