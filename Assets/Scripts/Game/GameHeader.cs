using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameHeader : MonoBehaviour {

  public CanvasGroup group;
  public Text timeLeftLabel;
  public Text turnsLeftLabel;

  [Header("Pause Resume")]
  public Image pauseImage;
  public Sprite pauseSprite;
  public Sprite resumeSprite;

  [Header("Word Header")]
  public Image backgroundImage;
  public Color drawColor;
  public Color someoneGuessedColor;
  public Color youGuessedColor;
  public Text wordLabel;

  private Color _defaultBackgroundColor;

  private void Start() {
    _defaultBackgroundColor = backgroundImage.color;
  }

  void Update() {
    //Show or hide controls
    switch (GameCoordinator.instance.gameState) {
      case GameCoordinator.GameState.Lobby:
        group.alpha = 0;
        break;
      case GameCoordinator.GameState.ClassicGame:
        group.alpha = 1;
        break;
    }

    if (GameCoordinator.instance.isGamePaused) {
      pauseImage.sprite = resumeSprite;
    } else {
      pauseImage.sprite = pauseSprite;
    }

    float timeLeft = Mathf.Max(0, GameCoordinator.instance.timeLeft + 0.999f);
    int timeLeftMinutes = Mathf.FloorToInt(timeLeft / 60);
    int timeLeftSeconds = Mathf.FloorToInt(timeLeft - timeLeftMinutes * 60);
    timeLeftLabel.text = timeLeftMinutes + ":" + timeLeftSeconds.ToString().PadLeft(2, '0');

    //Set the background color
    {
      Color color = _defaultBackgroundColor;

      switch (GameCoordinator.instance.gameState) {
        case GameCoordinator.GameState.ClassicGame:
          if (GameCoordinator.instance.drawingPlayer == Player.local) {
            color = drawColor;
          }

          if (Player.all.Any(p => p.hasGuessed)) {
            if (GameCoordinator.instance.drawingBoard == Player.local) {
              color = youGuessedColor;
            } else {
              color = someoneGuessedColor;
            }
          }

          if (Player.local.hasGuessed) {
            color = youGuessedColor;
          }
          break;
      }

      backgroundImage.color = color;
    }

    //Set the main word label
    switch (GameCoordinator.instance.gameState) {
      case GameCoordinator.GameState.Lobby:
        wordLabel.text = "";
        break;
      case GameCoordinator.GameState.ClassicGame:
        if (GameCoordinator.instance.drawingPlayer == Player.local && GameCoordinator.instance.currentWord != "") {
          string[] tokens = GameCoordinator.instance.currentWord.Split();
          wordLabel.text = string.Join(" ", tokens.Select(t => char.ToUpper(t[0]) + t.Substring(1)).ToArray());
        } else {
          wordLabel.text = "";
        }
        break;
    }

    //Set the turns left label
    switch (GameCoordinator.instance.gameState) {
      case GameCoordinator.GameState.Lobby:
        turnsLeftLabel.text = "";
        break;
      case GameCoordinator.GameState.ClassicGame:
        turnsLeftLabel.text = GameCoordinator.instance.turnsLeft.ToString();
        break;
    }
  }

  public void OnClickPauseUnpauseButton() {
    if (GameCoordinator.instance.isGamePaused) {
      Player.local.CmdUnpauseGame();
    } else {
      Player.local.CmdPauseGame();
    }
  }

  public void OnClickRejectWord() {
    Player.local.CmdRejectWord(Player.local.netId);
  }
}
