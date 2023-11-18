using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
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

    public NetworkVariable<string> GameName;
    public NetworkVariable<bool> IsInGame;
    public NetworkVariable<bool> HasGuessed;
    public NetworkVariable<int> Score;
    public NetworkVariable<bool> TimerHasReachedZero;

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

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        if (IsLocalPlayer) {
            Local = this;
        }

        ChangeNameRpc(_namePref.Value);
    }

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();

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
        if (IsLocalPlayer) {
            float timeLeft = GameCoordinator.instance.TimeLeft;

            if (timeLeft <= 0 && _prevTimeLeft > 0) {
                NotifyTimerReachedZeroRpc();
            }

            _prevTimeLeft = timeLeft;
        }
    }

    [ServerRpc]
    private void ChangeNameRpc(string name) {
        GameName.Value = name;
    }

    [ServerRpc]
    private void NotifyTimerReachedZeroRpc() {
        TimerHasReachedZero.Value = true;
    }

    [ServerRpc]
    public void RejectWordRpc(ulong clickerId) {
        GameCoordinator.instance.RejectCurrentWord(clickerId);
    }

    [ServerRpc]
    public void PauseGameRpc() {
        GameCoordinator.instance.SubmitPauseGame();
    }

    [ServerRpc]
    public void UnpauseGameRpc() {
        GameCoordinator.instance.SubmitResumeGame();
    }

    [ServerRpc]
    public void DrawRpc(BrushAction brush) {
        GameCoordinator.instance.SubmitBrush(this, brush);
    }

    [ServerRpc]
    private void AddMessageRpc(Message msg) {
        GameCoordinator.instance.SubmitMessage(this, msg);
    }




    [ClientRpc]
    public void UpdateNamePreferenceRpc(string name, ClientRpcParams clientRpcParams = default) {
        name = name.Trim();
        name = name.Substring(0, Mathf.Min(24, name.Length));
        _namePref.Value = name;
    }

    [ClientRpc]
    public void ExecuteClientMessageRpc(Message message, ClientRpcParams clientRpcParams = default) {
        if (tryParseLocalMessage(message)) {
            return;
        }

        //Else if we couldn't process the message, send it to the server
        AddMessageRpc(message);
    }

    private bool tryParseLocalMessage(Message message) {
        string[] tokens = message.text.Split().Where(t => t.Length > 0).ToArray();
        tokens[0] = tokens[0].ToLower();

        if (tokens[0] == "/quit") {
            NetworkManager.Singleton.Shutdown();
            return true;
        }

        if (tokens[0] == "/info") {
            AddMessageRpc(Message.User("\n" +
                                       B("Persistent Data Path:\n") +
                                       Application.persistentDataPath + "\n" +
                                       B("Word Bank Path:\n") +
                                       WordBankManager.BankPath));
            return true;
        }

        return false;
    }

}
