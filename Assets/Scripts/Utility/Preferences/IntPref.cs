using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Pref Object/Int", order = 900)]
public class IntPref : ScriptableObject {

  [SerializeField]
  private string key;

  [SerializeField]
  private int defaultValue;

  public int value {
    get {
      return PlayerPrefs.GetInt(key, defaultValue);
    }
    set {
      PlayerPrefs.SetInt(key, value);
    }
  }
}
