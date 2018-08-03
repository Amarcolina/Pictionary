using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Pref Object/Int", order = 900)]
public class IntPref : ScriptableObject {

  [SerializeField]
  [FormerlySerializedAs("key")]
  private string _key;

  [SerializeField]
  [FormerlySerializedAs("defaultValue")]
  private int _defaultValue;

  public int Value {
    get {
      return PlayerPrefs.GetInt(_key, _defaultValue);
    }
    set {
      PlayerPrefs.SetInt(_key, value);
    }
  }
}
