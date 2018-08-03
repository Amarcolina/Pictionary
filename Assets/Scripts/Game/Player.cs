using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using static RichTextUtility;

public class Player : NetworkBehaviour {
  public const string NAME_PREF_KEY = "PlayerNamePreference";

  public static Player Local;
  public static List<Player> All = new List<Player>();
  public static IEnumerable<Player> InGame = All.Where(p => p._isInGame);

  public static Action OnPlayerChange;

  [SerializeField]
  [FormerlySerializedAs("namePref")]
  private StringPref _namePref;

  [SyncVar]
  [SerializeField]
  [FormerlySerializedAs("gameName")]
  private string _gameName = "Unnamed";
  public string GameName {
    get { return _gameName; }
    set { _gameName = value; }
  }

  [SyncVar]
  [SerializeField]
  [FormerlySerializedAs("isInGame")]
  private bool _isInGame;
  public bool IsInGame {
    get { return _isInGame; }
    set { _isInGame = value; }
  }

  [SyncVar]
  [SerializeField]
  [FormerlySerializedAs("hasGuessed")]
  private bool _hasGuessed;
  public bool HasGuessed {
    get { return _hasGuessed; }
    set { _hasGuessed = value; }
  }

  [SyncVar]
  [SerializeField]
  [FormerlySerializedAs("score")]
  private int _score = 0;
  public int Score {
    get { return _score; }
    set { _score = value; }
  }

  [SyncVar]
  [SerializeField]
  [FormerlySerializedAs("timerHasReachedZero")]
  private bool _timerHasReachedZero;
  public bool TimerHasReachedZero {
    get { return _timerHasReachedZero; }
    set { _timerHasReachedZero = value; }
  }

  [NonSerialized]
  public float guessTime;

  private float _prevTimeLeft = 100;

  private void Awake() {
    All.Add(this);

    if (OnPlayerChange != null) {
      try {
        OnPlayerChange();
      } catch (Exception e) {
        Debug.LogException(e);
      }
    }
  }

  private void Start() {
    if (isLocalPlayer) {
      Local = this;
    }

    CmdChangeName(_namePref.Value);
  }

  private void OnDestroy() {
    All.Remove(this);

    if (Local == this) {
      Local = null;
    }

    if (OnPlayerChange != null) {
      try {
        OnPlayerChange();
      } catch (Exception e) {
        Debug.LogException(e);
      }
    }
  }

  private void Update() {
    if (isLocalPlayer) {
      float timeLeft = GameCoordinator.instance.TimeLeft;

      if (timeLeft <= 0 && _prevTimeLeft > 0) {
        CmdNotifyTimerReachedZero();
      }

      _prevTimeLeft = timeLeft;
    }
  }

  [Command]
  private void CmdChangeName(string name) {
    _gameName = name;
  }

  [Command]
  private void CmdNotifyTimerReachedZero() {
    _timerHasReachedZero = true;
  }

  [Command]
  public void CmdRejectWord(NetworkInstanceId clickerId) {
    GameCoordinator.instance.RejectCurrentWord(clickerId);
  }

  [Command]
  public void CmdPauseGame() {
    GameCoordinator.instance.SubmitPauseGame();
  }

  [Command]
  public void CmdUnpauseGame() {
    GameCoordinator.instance.SubmitResumeGame();
  }


  [Command]
  public void CmdDraw(BrushAction brush) {
    GameCoordinator.instance.SubmitBrush(this, brush);
  }

  [ClientRpc]
  public void RpcUpdateNamePreference(string name) {
    name = name.Trim();
    name = name.Substring(0, Mathf.Min(64, name.Length));
    _namePref.Value = name;
  }

  [Client]
  public void ExecuteClientMessage(Message message) {
    if (tryParseLocalMessage(message)) {
      return;
    }

    //Else if we couldn't process the message, send it to the server
    CmdAddMessage(message);
  }


  [Command]
  private void CmdAddMessage(Message msg) {
    GameCoordinator.instance.SubmitMessage(this, msg);
  }

  private bool tryParseLocalMessage(Message message) {
    string[] tokens = message.text.Split().Where(t => t.Length > 0).ToArray();
    tokens[0] = tokens[0].ToLower();

    if (tokens[0] == "/quit") {
      NetworkManager.singleton.StopHost();
      return true;
    }

    if (tokens[0] == "/info") {
      CmdAddMessage(Message.User("\n" +
                                 B("Persistent Data Path:\n") +
                                 Application.persistentDataPath + "\n" +
                                 B("Word Bank Path:\n") +
                                 WordBankManager.BankPath));
      return true;
    }

    return false;
  }

}
