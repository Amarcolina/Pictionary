using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Pref Object/String", order = 900)]
public class StringPref : ScriptableObject {

  [SerializeField]
  private string key;

  [SerializeField]
  private string defaultValue;

  public string value {
    get {
      return PlayerPrefs.GetString(key, defaultValue);
    }
    set {
      PlayerPrefs.GetString(key, value);
    }
  }
}

