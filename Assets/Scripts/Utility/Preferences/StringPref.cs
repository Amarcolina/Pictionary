using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Pref Object/String", order = 900)]
public class StringPref : ScriptableObject {

  [SerializeField]
  [FormerlySerializedAs("key")]
  private string _key;

  [SerializeField]
  [FormerlySerializedAs("defaultValue")]
  private string _defaultValue;

  public string Value {
    get {
      return PlayerPrefs.GetString(_key, _defaultValue);
    }
    set {
      PlayerPrefs.SetString(_key, value);
    }
  }
}

