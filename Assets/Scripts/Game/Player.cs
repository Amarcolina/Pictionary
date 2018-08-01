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

  public StringPref namePref;

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
      try {
        OnPlayerChange();
      } catch (Exception e) {
        Debug.LogException(e);
      }
    }
  }

  private void Start() {
    if (isLocalPlayer) {
      local = this;
    }

    CmdChangeName(namePref.value);
  }

  private void OnDestroy() {
    all.Remove(this);

    if (local == this) {
      local = null;
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
    namePref.value = name;
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

    return false;
  }

}
