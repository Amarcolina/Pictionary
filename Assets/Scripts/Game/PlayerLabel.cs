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
        _nameLabel.text = player.GameName.Value.Value;
        Color color = _defaultColor;

        if (player.Score.Value > 0) {
            _scoreLabel.text = player.Score.Value.ToString();
        } else {
            _scoreLabel.text = "";
        }

        if (GameCoordinator.instance.DrawingPlayer == player) {
            color = _drawingColor;
        }

        _nameLabel.color = color;
        _scoreLabel.color = color;
    }
}
