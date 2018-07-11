using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NamePreference : MonoBehaviour {

  public InputField field;

  void Start() {
    field.text = Player.playerName;
  }

  public void OnUpdateName(string name) {
    Player.playerName = name;
  }
}
