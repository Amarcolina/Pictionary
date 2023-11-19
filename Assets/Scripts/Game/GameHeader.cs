using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class GameHeader : MonoBehaviour {

  [SerializeField]
  [FormerlySerializedAs("group")]
  private CanvasGroup _group;

  [SerializeField]
  [FormerlySerializedAs("timeLeftLabel")]
  private Text _timeLeftLabel;

  [SerializeField]
  [FormerlySerializedAs("turnsLeftLabel")]
  private Text _turnsLeftLabel;

  [Header("Pause Resume")]
  [SerializeField]
  [FormerlySerializedAs("pauseImage")]
  private Image _pauseImage;

  [SerializeField]
  [FormerlySerializedAs("pauseSprite")]
  private Sprite _pauseSprite;

  [SerializeField]
  [FormerlySerializedAs("resumeSprite")]
  private Sprite _resumeSprite;

  [Header("Word Header")]
  [SerializeField]
  [FormerlySerializedAs("backgroundImage")]
  private Image _backgroundImage;

  [SerializeField]
  [FormerlySerializedAs("drawColor")]
  private Color _drawColor;

  [SerializeField]
  [FormerlySerializedAs("someoneGuessedColor")]
  private Color _someoneGuessedColor;

  [SerializeField]
  [FormerlySerializedAs("youGuessedColor")]
  private Color _youGuessedColor;

  [SerializeField]
  [FormerlySerializedAs("wordLabel")]
  private Text _wordLabel;

  private Color _defaultBackgroundColor;

  private void Start() {
    _defaultBackgroundColor = _backgroundImage.color;
  }

  void Update() {
    //Show or hide controls
    switch (GameCoordinator.instance.CurrentState.Value) {
      case GameCoordinator.GameState.Lobby:
        _group.alpha = 0;
        break;
      case GameCoordinator.GameState.ClassicGame:
        _group.alpha = 1;
        break;
    }

    if (GameCoordinator.instance.IsGamePaused.Value) {
      _pauseImage.sprite = _resumeSprite;
    } else {
      _pauseImage.sprite = _pauseSprite;
    }

    float timeLeft = Mathf.Max(0, GameCoordinator.instance.TimeLeft + 0.999f);
    int timeLeftMinutes = Mathf.FloorToInt(timeLeft / 60);
    int timeLeftSeconds = Mathf.FloorToInt(timeLeft - timeLeftMinutes * 60);
    _timeLeftLabel.text = timeLeftMinutes + ":" + timeLeftSeconds.ToString().PadLeft(2, '0');

    //Set the background color
    {
      Color color = _defaultBackgroundColor;

      switch (GameCoordinator.instance.CurrentState.Value) {
        case GameCoordinator.GameState.ClassicGame:
          if (GameCoordinator.instance.DrawingPlayer == Player.Local) {
            color = _drawColor;
          }

          if (Player.All.Any(p => p.HasGuessed.Value)) {
            if (GameCoordinator.instance.DrawingBoard == Player.Local) {
              color = _youGuessedColor;
            } else {
              color = _someoneGuessedColor;
            }
          }

          if (Player.Local.HasGuessed.Value) {
            color = _youGuessedColor;
          }
          break;
      }

      _backgroundImage.color = color;
    }

    //Set the main word label
    switch (GameCoordinator.instance.CurrentState.Value) {
      case GameCoordinator.GameState.Lobby:
        _wordLabel.text = "";
        break;
      case GameCoordinator.GameState.ClassicGame:
        if (GameCoordinator.instance.DrawingPlayer == Player.Local && GameCoordinator.instance.CurrentWord != "") {
          string[] tokens = GameCoordinator.instance.CurrentWord.Split();
          _wordLabel.text = string.Join(" ", tokens.Select(t => char.ToUpper(t[0]) + t.Substring(1)).ToArray());
        } else {
          _wordLabel.text = "";
        }
        break;
    }

    //Set the turns left label
    switch (GameCoordinator.instance.CurrentState.Value) {
      case GameCoordinator.GameState.Lobby:
        _turnsLeftLabel.text = "";
        break;
      case GameCoordinator.GameState.ClassicGame:
        _turnsLeftLabel.text = GameCoordinator.instance.TurnsLeft.ToString();
        break;
    }
  }

  public void OnClickPauseUnpauseButton() {
    if (GameCoordinator.instance.IsGamePaused.Value) {
      Player.Local.UnpauseGameServerRpc();
    } else {
      Player.Local.PauseGameServerRpc();
    }
  }

  public void OnClickRejectWord() {
    Player.Local.RejectWordServerRpc(Player.Local.NetworkObjectId);
  }
}
