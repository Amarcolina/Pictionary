using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;

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

  [Header("Game Data")]
  [SyncVar, SerializeField]
  private GameState _currentState = GameState.Lobby;
  public GameState CurrentState {
    get { return _currentState; }
  }

  [SyncVar, SerializeField]
  private bool _isGamePaused = false;
  public bool IsGamePaused {
    get { return _isGamePaused; }
  }

  [SyncVar, SerializeField]
  private int _turnsLeft;
  public int TurnsLeft {
    get { return _turnsLeft; }
  }

  #endregion

  #region PUBLIC API

  private uint _drawingPlayerId; //networkinstanceid
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
        if (player.netId.Value == _drawingPlayerId) {
          return player;
        }
      }
      return null;
    }
  }

  [Server]
  public void SubmitPauseGame() {
    switch (_currentState) {
      case GameState.Lobby:
        lobbySubmitPauseGame();
        break;
      case GameState.ClassicGame:
        classicSubmitPauseGame();
        break;
    }
  }

  [Server]
  public void SubmitResumeGame() {
    switch (_currentState) {
      case GameState.Lobby:
        lobbySubmitResumeGame();
        break;
      case GameState.ClassicGame:
        classicSubmitResumeGame();
        break;
    }
  }

  [Server]
  public void SubmitMessage(Player player, Message message) {
    if (tryParseUserCommand(player, message.text)) {
      return;
    }

    switch (_currentState) {
      case GameState.Lobby:
        lobbySubmitMessage(player, message);
        return;
      case GameState.ClassicGame:
        classicSubmitMessage(player, message);
        return;
    }
  }

  [Server]
  public void SubmitBrush(Player player, BrushAction brush) {
    if (CanPlayerDraw(player)) {
      _drawingBoard.ApplyBrushAction(brush);
    }
  }

  public bool CanPlayerDraw(Player player) {
    switch (_currentState) {
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
    if (isServer) {
      StartCoroutine(updateTimeCoroutine());
      StartLobby();
    }
  }

  private void Update() {
    switch (_currentState) {
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
    brush.drawerId = Player.Local.netId;

    if (CanPlayerDraw(Player.Local)) {
      _drawingBoard.PredictBrushAction(brush);
      Player.Local.CmdDraw(brush);
    }
  }

  private IEnumerator updateTimeCoroutine() {
    var wait1Second = new WaitForSeconds(1);
    while (true) {
      yield return wait1Second;
      RpcUpdateGameTime(Time.realtimeSinceStartup, forceUpdate: false);
      RpcUpdateTimeLeft(_serverTimeLeft, forceUpdate: false);
    }
  }

  [ClientRpc]
  private void RpcUpdateGameTime(float time, bool forceUpdate) {
    GameTime.Update(time, forceUpdate);
  }

  [ClientRpc]
  private void RpcUpdateTimeLeft(float time, bool forceUpdate) {
    TimeLeft.Update(time, forceUpdate);
  }

  [Server]
  private bool tryParseUserCommand(Player player, string command) {
    string[] tokens = command.Split().Where(t => t.Length > 0).ToArray();
    tokens[0] = tokens[0].ToLower();

    if (tokens[0] == "/name") {
      string prevName = player.GameName;
      string newName = string.Join(" ", tokens.Skip(1).ToArray()).Trim();

      if (newName.Length == 0) {
        MessageBoard.TargetSubmitMessage(player.connectionToClient, Message.Server("Cannot change your name to nothing."));
        return true;
      }

      player.GameName = newName;
      player.RpcUpdateNamePreference(newName);
      MessageBoard.RpcSubmitMessage(Message.Server(prevName + " changed their name to " + newName));
      return true;
    }

    if (tokens[0] == "/help") {
      MessageBoard.TargetSubmitMessage(player.connectionToClient, new Message() {
        netId = NetworkInstanceId.Invalid,
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
    _currentState = GameState.Lobby;

    foreach (var player in Player.All) {
      player.IsInGame = false;
    }

    _currentWordClient = "";
  }

  private void lobbyUpdate() { }

  [Server]
  private void lobbySubmitMessage(Player player, Message message) {
    if (lobbyTryParseCommand(player, message.text)) {
      return;
    }

    //All players can message in the lobby
    MessageBoard.RpcSubmitMessage(message);
  }

  [Server]
  private void lobbySubmitPauseGame() { }

  [Server]
  private void lobbySubmitResumeGame() {
    _isGamePaused = false;
  }

  [Server]
  private bool lobbyTryParseCommand(Player player, string text) {
    string[] tokens = text.Split().Where(t => t.Length > 0).ToArray();
    if (tokens.Length == 0) {
      return false;
    }

    string command = tokens[0].ToLower();
    if (command == "/play") {
      if (player.isServer) {
        StartClassicGame();
      } else {
        MessageBoard.TargetSubmitMessage(player.connectionToClient, Message.Server("Only the server can start a game."));
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

  [Server]
  public void StartClassicGame() {
    Debug.Log("Starting classic game...");
    _currentState = GameState.ClassicGame;

    //Join all players
    foreach (var player in Player.All) {
      player.IsInGame = true;
      player.Score = 0;
    }

    _drawingPlayerId = Player.All[Random.Range(0, Player.All.Count)].netId.Value;
    RpcUpdateDrawingPlayer(_drawingPlayerId);

    _turnsLeft = _turnsPerGame.Value;
    while ((_turnsLeft % Player.All.Count) != 0) {
      _turnsLeft++;
    }

    startNextTurn();
  }

  [Server]
  public void RejectCurrentWord(NetworkInstanceId clickerId) {
    Player clickingPlayer = Player.All.FirstOrDefault(p => p.netId == clickerId);
    if (clickingPlayer == null) {
      Debug.LogError("Could not find player with id " + clickerId);
      return;
    }

    //If the clicking player is also the drawing player
    //And as long as nobody has guessed yet
    if (clickingPlayer == DrawingPlayer && Player.All.All(p => !p.HasGuessed)) {
      //First we let everybody know the word has been rejected, and what the word was
      MessageBoard.RpcSubmitMessage(Message.Server(clickingPlayer.GameName + " has rejected the word " + CurrentWord + "."));

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
    if (isServer) {
      if (DrawingPlayer == null) {
        finishTurn();
      }

      if (!_isGamePaused) {
        if (Input.GetKey(KeyCode.F2)) {
          _serverTimeLeft = Mathf.MoveTowards(_serverTimeLeft, 0, Time.deltaTime * 10);
        } else {
          _serverTimeLeft = Mathf.MoveTowards(_serverTimeLeft, 0, Time.deltaTime);
        }
      }

      if (Player.All.All(p => p.TimerHasReachedZero)) {
        finishTurn();
      }
    }
  }

  [Server]
  private void classicSubmitPauseGame() {
    _isGamePaused = true;
  }

  [Server]
  private void classicSubmitResumeGame() {
    _isGamePaused = false;
  }

  [Server]
  private void classicSubmitMessage(Player player, Message message) {
    //Only players who are currently playing can message
    if (!player.IsInGame) {
      return;
    }

    //Allow the server to stop the game if they want to
    if (message.text == "/stop") {
      if (player.isServer) {
        MessageBoard.RpcSubmitMessage(Message.Server("The server stopped the game."));
        StartLobby();
      } else {
        MessageBoard.TargetSubmitMessage(player.connectionToClient, Message.Server("Only the server can stop an in-progress game."));
      }
      return;
    }

    //Drawing player cannot message at all
    if (player == DrawingPlayer) {
      return;
    }

    //Players that have already guessed cannot message anymore
    if (player.HasGuessed) {
      return;
    }

    //If the guess is correct
    if (WordUtility.DoesGuessMatch(message.text, CurrentWord)) {
      _currentWordTransaction.NotifyGuess(1 - message.timeLeft / _timePerTurn.Value);

      //Send the message only to the player who guessed
      MessageBoard.TargetSubmitMessage(player.connectionToClient, message);

      //Don't submit the actual message to the rest, just tell everyone that they have guessed correctly
      MessageBoard.RpcSubmitMessage(Message.Server(player.GameName + " has guessed!"));

      player.guessTime = message.boardDisplayTime;
      player.HasGuessed = true;

      //If all guessing players have guessed, start next turn
      if (Player.All.Where(p => p.IsInGame && p != DrawingPlayer).All(p => p.HasGuessed)) {
        finishTurn();
        return;
      }

      //If this is the first person to guess
      //Set the time to be 15 seconds remaining!
      if (Player.All.Count(p => p.HasGuessed) == 1) {
        _serverTimeLeft = _endOfGameDelay.Value;
        RpcUpdateTimeLeft(_serverTimeLeft, forceUpdate: true);
        return;
      }

      return;
    }

    //If the guess is close it is not broadcast to the rest of the players
    if (WordUtility.IsGuessClose(message.text, CurrentWord)) {
      MessageBoard.TargetSubmitMessage(player.connectionToClient, message);
      MessageBoard.TargetSubmitMessage(player.connectionToClient, Message.Server("Your guess is close"));
      return;
    }

    MessageBoard.RpcSubmitMessage(message);
  }

  private bool classicCanPlayerDraw(Player player) {
    //Only the currently drawing player can draw :D
    return player == DrawingPlayer;
  }

  [Server]
  private void finishTurn() {
    Debug.Log("Finishing turn...");

    Assert.IsNotNull(_currentWordTransaction, "We should always be undergoing a word transaction before finishing a turn.");
    MessageBoard.RpcSubmitMessage(Message.Server("The word was " + CurrentWord));

    _currentWordTransaction.CompleteTransaction();
    _currentWordTransaction = null;
    _wordBankManager.SaveActiveWordBank();

    if (Player.All.Any(p => p.HasGuessed)) {
      //Drawing player gets 9 points as long as someone has guessed
      //Plus 1 point for every player who guessed (which is at least 1)
      DrawingPlayer.Score += 9 + Player.All.Count(p => p.HasGuessed);

      //All other players get points based on the order they guessed
      //First to guess gets 10, next gets 9, and so on
      //A player that guesses always gets at least 1 point
      int points = 10;
      foreach (var player in Player.All.Where(p => p.HasGuessed).OrderBy(p => p.guessTime)) {
        player.Score += points;
        points = Mathf.Max(1, points - 1);
      }
    }

    //Start the lobby if there are not enough players to play a game
    if (Player.All.Count(p => p.IsInGame) <= 1) {
      Debug.Log("Starting lobby because there were not enough players...");
      StartLobby();
      return;
    }

    //Decrement the turn counter
    //And end the game if we have reached zero turns!
    _turnsLeft--;
    if (_turnsLeft == 0) {
      int maxScore = Player.InGame.Select(p => p.Score).Max();
      var winners = Player.InGame.Where(p => p.Score == maxScore);
      if (winners.Count() == 1) {
        var winner = winners.Single();
        MessageBoard.RpcSubmitMessage(Message.Server("Player " + winner.GameName + " wins!"));
      } else {
        MessageBoard.RpcSubmitMessage(Message.Server("Game is a tie between " + string.Join(" and ", winners.Select(p => p.GameName).ToArray()) + "!"));
      }
      StartLobby();
      return;
    }

    //Skip to next player who is ingame
    {
      int currIndex = Player.All.IndexOf(DrawingPlayer);
      do {
        currIndex = (currIndex + 1) % Player.All.Count;
      } while (!Player.All[currIndex].IsInGame);

      _drawingPlayerId = Player.All[currIndex].netId.Value;
      RpcUpdateDrawingPlayer(_drawingPlayerId);
    }

    startNextTurn();
  }

  [Server]
  private void startNextTurn() {
    Debug.Log("Starting new turn...");

    Assert.IsNull(_currentWordTransaction, "The current transaction should be null before we start a new one.");
    _currentWordTransaction = _wordBankManager.Bank.BeginWord(new BasicSelector());
    _currentWordTransaction.SetPlayerCount(Player.InGame.Count());

    TargetUpdateCurrentWord(DrawingPlayer.connectionToClient, CurrentWord);

    foreach (var player in Player.All) {
      player.HasGuessed = false;
      player.TimerHasReachedZero = false;
    }

    DrawingBoard.ClearAndReset();

    _serverTimeLeft = _timePerTurn.Value;
    RpcUpdateTimeLeft(_serverTimeLeft, forceUpdate: true);
  }

  [TargetRpc]
  private void TargetUpdateCurrentWord(NetworkConnection conn, string word) {
    _currentWordClient = word;
  }

  [ClientRpc]
  private void RpcUpdateDrawingPlayer(uint id) {
    _drawingPlayerId = id;
  }

  #endregion

  public enum GameState {
    Lobby,
    ClassicGame
  }
}
