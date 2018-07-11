using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Player : NetworkBehaviour {
  public const string NAME_PREF_KEY = "PlayerNamePreference";

  public static Player local;
  public static List<Player> all = new List<Player>();
  public static IEnumerable<Player> inGame = all.Where(p => p.isInGame);

  public static Action OnPlayerChange;

  public static string playerName {
    get {
      return PlayerPrefs.GetString(NAME_PREF_KEY, "Unnamed");
    }
    set {
      value = value.Trim();
      value = value.Substring(0, Mathf.Min(64, value.Length));
      PlayerPrefs.SetString(NAME_PREF_KEY, value);
    }
  }

  [SyncVar]
  public string gameName = "Unnamed";

  [SyncVar]
  public bool isInGame;

  [SyncVar]
  public bool hasGuessed;

  [SyncVar]
  public int score = 0;

  [SyncVar]
  public bool timerHasReachedZero;

  [NonSerialized]
  public float guessTime;

  private float _prevTimeLeft = 100;

  private void Awake() {
    all.Add(this);

    if (OnPlayerChange != null) {
      OnPlayerChange();
    }
  }

  [ClientCallback]
  private void Start() {
    if (isLocalPlayer) {
      local = this;
    }

    CmdChangeName(playerName);
  }

  [ClientCallback]
  private void OnDestroy() {
    all.Remove(this);

    if (OnPlayerChange != null) {
      OnPlayerChange();
    }
  }

  [ClientCallback]
  private void Update() {
    if (isLocalPlayer) {
      float timeLeft = GameCoordinator.instance.timeLeft;

      if (timeLeft <= 0 && _prevTimeLeft > 0) {
        CmdNotifyTimerReachedZero();
      }

      _prevTimeLeft = timeLeft;
    }
  }

  [Command]
  private void CmdChangeName(string name) {
    gameName = name;
  }

  [Command]
  private void CmdNotifyTimerReachedZero() {
    timerHasReachedZero = true;
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
  public void CmdAddMessage(Message msg) {
    GameCoordinator.instance.SubmitMessage(this, msg);
  }

  [Command]
  public void CmdDraw(BrushAction brush) {
    GameCoordinator.instance.SubmitBrush(this, brush);
  }

  [ClientRpc]
  public void RpcUpdateNamePreference(string name) {
    playerName = name;
  }

}
