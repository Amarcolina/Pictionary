using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using Unity.Netcode;

public static class RpcSend {

    public static ClientRpcParams To(NetworkBehaviour behaviour) {
        return new ClientRpcParams() {
            Send = new ClientRpcSendParams() {
                TargetClientIds = new ulong[] { behaviour.NetworkObjectId }
            }
        };
    }
}

public class GameCoordinator : NetworkBehaviour {

    private static GameCoordinator _cachedInstance;
    public static GameCoordinator instance {
        get {
            if (_cachedInstance == null) {
                _cachedInstance = FindObjectOfType<GameCoordinator>();
            }
            return _cachedInstance;
        }
    }

    #region INSPECTOR

    [SerializeField]
    private WordBankManager _wordBankManager;

    [SerializeField]
    private MessageBoard _messageBoard;
    public MessageBoard MessageBoard {
        get { return _messageBoard; }
    }

    [SerializeField]
    private DrawingBoard _drawingBoard;
    public DrawingBoard DrawingBoard {
        get { return _drawingBoard; }
    }

    [SerializeField]
    private IntPref _turnsPerGame;

    [SerializeField]
    private IntPref _timePerTurn;

    [SerializeField]
    private IntPref _endOfGameDelay;

    public NetworkVariable<GameState> CurrentState;
    public NetworkVariable<bool> IsGamePaused;
    public NetworkVariable<int> TurnsLeft;

    #endregion

    #region PUBLIC API

    private ulong _drawingPlayerId; //networkinstanceid
    private WordTransaction _currentWordTransaction = null;
    private string _currentWordClient = "";
    public string CurrentWord {
        get {
            if (_currentWordTransaction != null) {
                return _currentWordTransaction.word;
            } else {
                return _currentWordClient;
            }
        }
    }

    public NetworkedTime GameTime { get; private set; }
    public NetworkedTime TimeLeft { get; private set; }

    public Player DrawingPlayer {
        get {
            foreach (var player in Player.All) {
                if (player.NetworkObjectId == _drawingPlayerId) {
                    return player;
                }
            }
            return null;
        }
    }

    public void SubmitPauseGame() {
        switch (CurrentState.Value) {
            case GameState.Lobby:
                lobbySubmitPauseGame();
                break;
            case GameState.ClassicGame:
                classicSubmitPauseGame();
                break;
        }
    }

    public void SubmitResumeGame() {
        switch (CurrentState.Value) {
            case GameState.Lobby:
                lobbySubmitResumeGame();
                break;
            case GameState.ClassicGame:
                classicSubmitResumeGame();
                break;
        }
    }

    public void SubmitMessage(Player player, Message message) {
        if (tryParseUserCommand(player, message.text)) {
            return;
        }

        switch (CurrentState.Value) {
            case GameState.Lobby:
                lobbySubmitMessage(player, message);
                return;
            case GameState.ClassicGame:
                classicSubmitMessage(player, message);
                return;
        }
    }

    public void SubmitBrush(Player player, BrushAction brush) {
        if (CanPlayerDraw(player)) {
            _drawingBoard.ApplyBrushAction(brush);
        }
    }

    public bool CanPlayerDraw(Player player) {
        switch (CurrentState.Value) {
            case GameState.Lobby:
                return lobbyCanPlayerDraw(player);
            case GameState.ClassicGame:
                return classicCanPlayerDraw(player);
        }

        return false;
    }

    #endregion

    #region UNITY MESSAGES

    private void Awake() {
        _cachedInstance = this;

        GameTime = new NetworkedTime() {
            Rate = 1
        };

        TimeLeft = new NetworkedTime() {
            Rate = -1
        };
    }

    private void OnEnable() {
        Paintbrush.OnDraw += OnDraw;
    }

    private void OnDisable() {
        Paintbrush.OnDraw -= OnDraw;
    }

    private void Start() {
        if (IsServer) {
            StartCoroutine(updateTimeCoroutine());
            StartLobby();
        }
    }

    private void Update() {
        switch (CurrentState.Value) {
            case GameState.Lobby:
                lobbyUpdate();
                break;
            case GameState.ClassicGame:
                classicUpdate();
                break;
        }
    }
    #endregion

    #region GENERAL IMPLEMENTATION
    private float _serverTimeLeft;

    private void OnDraw(BrushAction brush) {
        brush.drawerId = Player.Local.NetworkObjectId;

        if (CanPlayerDraw(Player.Local)) {
            _drawingBoard.PredictBrushAction(brush);
            Player.Local.DrawServerRpc(brush);
        }
    }

    private IEnumerator updateTimeCoroutine() {
        var wait1Second = new WaitForSeconds(1);
        while (true) {
            yield return wait1Second;
            RpcUpdateGameTimeClientRpc(Time.realtimeSinceStartup, forceUpdate: false);
            RpcUpdateTimeLeftClientRpc(_serverTimeLeft, forceUpdate: false);
        }
    }

    [ClientRpc]
    private void RpcUpdateGameTimeClientRpc(float time, bool forceUpdate) {
        GameTime.Update(time, forceUpdate);
    }

    [ClientRpc]
    private void RpcUpdateTimeLeftClientRpc(float time, bool forceUpdate) {
        TimeLeft.Update(time, forceUpdate);
    }

    private bool tryParseUserCommand(Player player, string command) {
        string[] tokens = command.Split().Where(t => t.Length > 0).ToArray();
        tokens[0] = tokens[0].ToLower();

        if (tokens[0] == "/name") {
            string prevName = player.GameName.Value;
            string newName = string.Join(" ", tokens.Skip(1).ToArray()).Trim();

            if (newName.Length == 0) {
                MessageBoard.SubmitMessageClientRpc(Message.Server("Cannot change your name to nothing."), RpcSend.To(player));
                return true;
            }

            player.GameName.Value = newName;

            player.UpdateNamePreferenceClientRpc(newName, RpcSend.To(player));

            MessageBoard.SubmitMessageClientRpc(Message.Server(prevName + " changed their name to " + newName));
            return true;
        }

        if (tokens[0] == "/help") {
            MessageBoard.SubmitMessageClientRpc(new Message() {
                netId = 1231231,
                boardDisplayTime = 0,
                color = new Color32(0, 0, 0, 0),
                bold = false,
                text =
              "<b>/help</b>\n" +
              "Shows the list of commands\n" +
              "<b>/play</b>\n" +
              "Starts a new classic game\n" +
              "<b>/stop</b>\n" +
              "Stops an in-progress game\n" +
              "<b>/name <new name></b>\n" +
              "Changes your name\n" +
              "<b>/quit</b>\n" +
              "Quits to the lobby"
            });
            return true;
        }

        return false;
    }

    #endregion

    #region LOBBY LOGIC

    public void StartLobby() {
        CurrentState.Value = GameState.Lobby;

        foreach (var player in Player.All) {
            player.IsInGame.Value = false;
        }

        _currentWordClient = "";
    }

    private void lobbyUpdate() { }

    private void lobbySubmitMessage(Player player, Message message) {
        if (lobbyTryParseCommand(player, message.text)) {
            return;
        }

        //All players can message in the lobby
        MessageBoard.SubmitMessageClientRpc(message);
    }

    private void lobbySubmitPauseGame() { }

    private void lobbySubmitResumeGame() {
        IsGamePaused.Value = false;
    }

    private bool lobbyTryParseCommand(Player player, string text) {
        string[] tokens = text.Split().Where(t => t.Length > 0).ToArray();
        if (tokens.Length == 0) {
            return false;
        }

        string command = tokens[0].ToLower();
        if (command == "/play") {
            if (player.IsServer) {
                StartClassicGame();
            } else {
                MessageBoard.SubmitMessageClientRpc(Message.Server("Only the server can start a game."), RpcSend.To(player));
            }
            return true;
        }

        return false;
    }

    private bool lobbyCanPlayerDraw(Player player) {
        return true;
    }

    #endregion

    #region CLASSIC GAME LOGIC

    public void StartClassicGame() {
        Debug.Log("Starting classic game...");
        CurrentState.Value = GameState.ClassicGame;

        //Join all players
        foreach (var player in Player.All) {
            player.IsInGame.Value = true;
            player.Score.Value = 0;
        }

        _drawingPlayerId = Player.All[Random.Range(0, Player.All.Count)].NetworkObjectId;
        UpdateDrawingPlayerClientRpc(_drawingPlayerId);

        TurnsLeft.Value = _turnsPerGame.Value;
        while ((TurnsLeft.Value % Player.All.Count) != 0) {
            TurnsLeft.Value++;
        }

        startNextTurn();
    }

    public void RejectCurrentWord(ulong clickerId) {
        Player clickingPlayer = Player.All.FirstOrDefault(p => p.NetworkObjectId == clickerId);
        if (clickingPlayer == null) {
            Debug.LogError("Could not find player with id " + clickerId);
            return;
        }

        //If the clicking player is also the drawing player
        //And as long as nobody has guessed yet
        if (clickingPlayer == DrawingPlayer && Player.All.All(p => !p.HasGuessed.Value)) {
            //First we let everybody know the word has been rejected, and what the word was
            MessageBoard.SubmitMessageClientRpc(Message.Server(clickingPlayer.GameName + " has rejected the word " + CurrentWord + "."));

            //Then we record the rejection in the current word transaction, and complete the transaction
            _currentWordTransaction.Reject(1.0f - _serverTimeLeft / _timePerTurn.Value);
            _currentWordTransaction.CompleteTransaction();
            _currentWordTransaction = null;
            _wordBankManager.SaveActiveWordBank();

            //Then we (re)start the turn
            startNextTurn();
        }
    }

    private void classicUpdate() {
        if (IsServer) {
            if (DrawingPlayer == null) {
                finishTurn();
            }

            if (!IsGamePaused.Value) {
                if (Input.GetKey(KeyCode.F2)) {
                    _serverTimeLeft = Mathf.MoveTowards(_serverTimeLeft, 0, Time.deltaTime * 10);
                } else {
                    _serverTimeLeft = Mathf.MoveTowards(_serverTimeLeft, 0, Time.deltaTime);
                }
            }

            if (Player.All.All(p => p.TimerHasReachedZero.Value)) {
                finishTurn();
            }
        }
    }

    private void classicSubmitPauseGame() {
        IsGamePaused.Value = true;
    }

    private void classicSubmitResumeGame() {
        IsGamePaused.Value = false;
    }

    private void classicSubmitMessage(Player player, Message message) {
        //Only players who are currently playing can message
        if (!player.IsInGame.Value) {
            return;
        }

        //Allow the server to stop the game if they want to
        if (message.text == "/stop") {
            if (player.IsServer) {
                MessageBoard.SubmitMessageClientRpc(Message.Server("The server stopped the game."));
                StartLobby();
            } else {
                MessageBoard.SubmitMessageClientRpc(Message.Server("Only the server can stop an in-progress game."), RpcSend.To(player));
            }
            return;
        }

        //Drawing player cannot message at all
        if (player == DrawingPlayer) {
            return;
        }

        //Players that have already guessed cannot message anymore
        if (player.HasGuessed.Value) {
            return;
        }

        //If the guess is correct
        if (WordUtility.DoesGuessMatch(message.text, CurrentWord)) {
            _currentWordTransaction.NotifyGuess(1 - message.timeLeft / _timePerTurn.Value);

            //Send the message only to the player who guessed
            MessageBoard.SubmitMessageClientRpc(message, RpcSend.To(player));

            //Don't submit the actual message to the rest, just tell everyone that they have guessed correctly
            MessageBoard.SubmitMessageClientRpc(Message.Server(player.GameName + " has guessed!"));

            player.guessTime = message.boardDisplayTime;
            player.HasGuessed.Value = true;

            //If all guessing players have guessed, start next turn
            if (Player.All.Where(p => p.IsInGame.Value && p != DrawingPlayer).All(p => p.HasGuessed.Value)) {
                finishTurn();
                return;
            }

            //If this is the first person to guess
            //Set the time to be 15 seconds remaining!
            if (Player.All.Count(p => p.HasGuessed.Value) == 1) {
                _serverTimeLeft = _endOfGameDelay.Value;
                RpcUpdateTimeLeftClientRpc(_serverTimeLeft, forceUpdate: true);
                return;
            }

            return;
        }

        //If the guess is close it is not broadcast to the rest of the players
        if (WordUtility.IsGuessClose(message.text, CurrentWord)) {
            MessageBoard.SubmitMessageClientRpc(message, RpcSend.To(player));
            MessageBoard.SubmitMessageClientRpc(Message.Server("Your guess is close"), RpcSend.To(player));
            return;
        }

        MessageBoard.SubmitMessageClientRpc(message);
    }

    private bool classicCanPlayerDraw(Player player) {
        //Only the currently drawing player can draw :D
        return player == DrawingPlayer;
    }

    private void finishTurn() {
        Debug.Log("Finishing turn...");

        Assert.IsNotNull(_currentWordTransaction, "We should always be undergoing a word transaction before finishing a turn.");
        MessageBoard.SubmitMessageClientRpc(Message.Server("The word was " + CurrentWord));

        _currentWordTransaction.CompleteTransaction();
        _currentWordTransaction = null;
        _wordBankManager.SaveActiveWordBank();

        if (Player.All.Any(p => p.HasGuessed.Value)) {
            //Drawing player gets 9 points as long as someone has guessed
            //Plus 1 point for every player who guessed (which is at least 1)
            DrawingPlayer.Score.Value += 9 + Player.All.Count(p => p.HasGuessed.Value);

            //All other players get points based on the order they guessed
            //First to guess gets 10, next gets 9, and so on
            //A player that guesses always gets at least 1 point
            int points = 10;
            foreach (var player in Player.All.Where(p => p.HasGuessed.Value).OrderBy(p => p.guessTime)) {
                player.Score.Value += points;
                points = Mathf.Max(1, points - 1);
            }
        }

        //Start the lobby if there are not enough players to play a game
        if (Player.All.Count(p => p.IsInGame.Value) <= 1) {
            Debug.Log("Starting lobby because there were not enough players...");
            StartLobby();
            return;
        }

        //Decrement the turn counter
        //And end the game if we have reached zero turns!
        TurnsLeft.Value--;
        if (TurnsLeft.Value == 0) {
            int maxScore = Player.InGame.Select(p => p.Score.Value).Max();
            var winners = Player.InGame.Where(p => p.Score.Value == maxScore);
            if (winners.Count() == 1) {
                var winner = winners.Single();
                MessageBoard.SubmitMessageClientRpc(Message.Server("Player " + winner.GameName + " wins!"));
            } else {
                MessageBoard.SubmitMessageClientRpc(Message.Server("Game is a tie between " + string.Join(" and ", winners.Select(p => p.GameName.Value).ToArray()) + "!"));
            }
            StartLobby();
            return;
        }

        //Skip to next player who is ingame
        {
            int currIndex = Player.All.IndexOf(DrawingPlayer);
            do {
                currIndex = (currIndex + 1) % Player.All.Count;
            } while (!Player.All[currIndex].IsInGame.Value);

            _drawingPlayerId = Player.All[currIndex].NetworkObjectId;
            UpdateDrawingPlayerClientRpc(_drawingPlayerId);
        }

        startNextTurn();
    }

    private void startNextTurn() {
        Debug.Log("Starting new turn...");

        Assert.IsNull(_currentWordTransaction, "The current transaction should be null before we start a new one.");
        _currentWordTransaction = _wordBankManager.Bank.BeginWord(new BasicSelector());
        _currentWordTransaction.SetPlayerCount(Player.InGame.Count());

        UpdateCurrentWordClientRpc(CurrentWord, RpcSend.To(DrawingPlayer));

        foreach (var player in Player.All) {
            player.HasGuessed.Value = false;
            player.TimerHasReachedZero.Value = false;
        }

        DrawingBoard.ClearAndReset();

        _serverTimeLeft = _timePerTurn.Value;
        RpcUpdateTimeLeftClientRpc(_serverTimeLeft, forceUpdate: true);
    }

    [ClientRpc]
    private void UpdateCurrentWordClientRpc(string word, ClientRpcParams clientRpcParams = default) {
        _currentWordClient = word;
    }

    [ClientRpc]
    private void UpdateDrawingPlayerClientRpc(ulong id) {
        _drawingPlayerId = id;
    }

    #endregion

    public enum GameState {
        Lobby,
        ClassicGame
    }
}
