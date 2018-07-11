using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerLabel : MonoBehaviour {

  [SerializeField]
  private Text _nameLabel;

  [SerializeField]
  private Text _scoreLabel;

  [SerializeField]
  private Color _defaultColor;

  [SerializeField]
  private Color _drawingColor;

  [SerializeField]
  private Color _spectatingColor;

  [NonSerialized]
  public Player player;

  private void Update() {
    _nameLabel.text = player.gameName;
    Color color = _defaultColor;

    switch (GameCoordinator.instance.gameState) {
      case GameCoordinator.GameState.Lobby:
        _scoreLabel.text = "";
        break;
      case GameCoordinator.GameState.ClassicGame:
        if (player.isInGame) {
          _scoreLabel.text = player.score.ToString();
          if (GameCoordinator.instance.drawingPlayer == player) {
            color = _drawingColor;
          }
        } else {
          _scoreLabel.text = "";
          color = _spectatingColor;
        }
        break;
    }

    _nameLabel.color = color;
    _scoreLabel.color = color;
  }
}
