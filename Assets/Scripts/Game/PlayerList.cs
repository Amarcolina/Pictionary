using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

public class PlayerList : NetworkBehaviour {

  [SerializeField]
  [FormerlySerializedAs("labelPrefab")]
  private PlayerLabel _labelPrefab;

  [SerializeField]
  [FormerlySerializedAs("labelAnchor")]
  private Transform _labelAnchor;

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

    foreach (var player in Player.All) {
      var spawned = Instantiate(_labelPrefab);
      spawned.player = player;

      spawned.transform.SetParent(_labelAnchor, worldPositionStays: false);
      spawned.gameObject.SetActive(true);

      _spawned.Add(spawned);
    }
  }
}
