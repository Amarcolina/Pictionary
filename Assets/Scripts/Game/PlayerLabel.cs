using System;
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
    _nameLabel.text = player.GameName;
    Color color = _defaultColor;

    switch (GameCoordinator.instance.CurrentState) {
      case GameCoordinator.GameState.Lobby:
        _scoreLabel.text = "";
        break;
      case GameCoordinator.GameState.ClassicGame:
        if (player.IsInGame) {
          _scoreLabel.text = player.Score.ToString();
          if (GameCoordinator.instance.DrawingPlayer == player) {
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
