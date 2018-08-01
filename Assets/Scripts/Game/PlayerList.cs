using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class PlayerList : NetworkBehaviour {

  public PlayerLabel labelPrefab;
  public Transform labelAnchor;

  private List<PlayerLabel> _spawned = new List<PlayerLabel>();

  private void OnEnable() {
    Player.OnPlayerChange += updatePlayerList;
    updatePlayerList();
  }

  private void OnDisable() {
    Player.OnPlayerChange -= updatePlayerList;
  }

  [Client]
  private void updatePlayerList() {
    foreach (var spawned in _spawned) {
      if (spawned != null) {
        DestroyImmediate(spawned.gameObject);
      }
    }
    _spawned.Clear();

    foreach (var player in Player.all) {
      var spawned = Instantiate(labelPrefab);
      spawned.player = player;

      spawned.transform.SetParent(labelAnchor, worldPositionStays: false);
      spawned.gameObject.SetActive(true);

      _spawned.Add(spawned);
    }
  }
}
