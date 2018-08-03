using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

[RequireComponent(typeof(InputField))]
public class StringPrefInput : MonoBehaviour {

  [SerializeField]
  [FormerlySerializedAs("preference")]
  private StringPref _preference;

  private InputField _field;

  private void Awake() {
    _field = GetComponent<InputField>();
  }

  private void OnEnable() {
    _field.text = _preference.Value.ToString();
    _field.onEndEdit.AddListener(onTextChange);
  }

  private void OnDisable() {
    _field.onEndEdit.RemoveListener(onTextChange);
  }

  private void onTextChange(string text) {
    _preference.Value = text;
  }
}
