using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public interface IWordSelector {
  string SelectWord(WordBank.WordData[] data, string[] entryHistory);
}

public class WordTransaction {
  public readonly string word;

  private bool _isTransactionComplete = false;
  private int _playerCount;

  private bool _isRejected = false;
  private float _rejectedTime = 0;

  private List<float> _guessTimes = new List<float>();
  private Action<WordBank.WordData.TurnData> _endWordDelegate;

  public WordTransaction(string word, Action<WordBank.WordData.TurnData> endWordDelegate) {
    this.word = word;
    _endWordDelegate = endWordDelegate;
  }

  public void CompleteTransaction() {
    if (_playerCount == 0) {
      throw new InvalidOperationException("Cannot complete the transaction if no players have been set.");
    }

    _isTransactionComplete = true;
    _endWordDelegate(new WordBank.WordData.TurnData() {
      WasRejected = _isRejected,
      RejectTime = _rejectedTime,
      TotalPlayers = _playerCount,
      GuessTimes = _guessTimes.ToArray()
    });
  }

  public void SetPlayerCount(int playerCount) {
    expectStillActive("SetPlayerCount");
    if (_playerCount != 0) {
      throw new InvalidOperationException("Cannot call SetPlayerCount more than once.");
    }
    if (playerCount <= 0) {
      throw new ArgumentException("The playerCount must be positive and nonzero.");
    }

    _playerCount = playerCount;
  }

  public void NotifyGuess(float guessTime) {
    expectStillActive("NotifyGuess");
    expectPlayerCount("NotifyGuess");
    expectNotRejected("NotifyGuess");

    if (_guessTimes.Count == _playerCount) {
      throw new InvalidOperationException("There have already been the maximum number of guesses reported.");
    }

    if (guessTime < 0 || guessTime > 1) {
      float newTime = Mathf.Clamp01(guessTime);
      Debug.LogWarning("The guess time of " + guessTime + " has been clamped to " + newTime);
      guessTime = newTime;
    }

    _guessTimes.Add(guessTime);
  }

  public void Reject(float time) {
    expectStillActive("Reject");
    expectPlayerCount("Reject");
    expectNotRejected("Reject");

    if (time < 0 || time > 1) {
      throw new ArgumentException("The reject time must be within the 0-1 range.");
    }

    _isRejected = true;
    _rejectedTime = time;
  }

  private void expectStillActive(string methodName) {
    if (_isTransactionComplete) {
      throw new InvalidOperationException("Cannot call " + methodName + " once the transaction has been completed.");
    }
  }

  private void expectPlayerCount(string methodName) {
    if (_playerCount == 0) {
      throw new InvalidOperationException("Must set the player count before calling " + methodName);
    }
  }

  private void expectNotRejected(string methodName) {
    if (_isRejected) {
      throw new InvalidOperationException("Cannot call " + methodName + " once the word has been rejected.");
    }
  }
}

[Serializable]
public class WordBank {

  /// <summary>
  /// All words in the bank, in no particular order
  /// </summary>
  [SerializeField]
  private WordData[] _wordData;

  /// <summary>
  /// The history of words played.  Each element is the word itself.
  /// The element at index 0 is the most recent word played.
  /// </summary>
  [SerializeField]
  private List<string> _entryHistory = new List<string>();

  [NonSerialized]
  private WordTransaction _currentTransaction = null;

  public WordBank(IEnumerable<string> words) {
    _wordData = words.Select(w => w.Trim()).
                      Select(w => w.ToLower()).
                      Select(w => new string(w.Where(c => char.IsLetter(c)).ToArray())).
                      Where(w => w != "").
                      Select(w => new WordData(w)).
                      ToArray();
  }

  public int WordCount {
    get {
      return _wordData.Length;
    }
  }

  /// <summary>
  /// Given a specific selector, select a word from the word bank to use.  Returns
  /// a WordTransaction that can be used to notify changes to the word that should 
  /// be reflected in the bank.
  /// 
  /// Once a transaction has begun, you cannot start any new transactions until you
  /// call EndWord.  Modifications to the word will not take effect unless EndWord
  /// is called.
  /// </summary>
  public WordTransaction BeginWord(IWordSelector selector) {
    if (_currentTransaction != null) {
      throw new InvalidOperationException("Cannot begin a new word transaction while one is currently in progress.");
    }

    string word = selector.SelectWord(_wordData.ToArray(), _entryHistory.ToArray());

    _currentTransaction = new WordTransaction(word, endWord);
    return _currentTransaction;
  }

  /// <summary>
  /// Ends the currently active transaction and stores the changes in the database.
  /// </summary>
  private void endWord(WordData.TurnData turnData) {
    _entryHistory.Insert(0, _currentTransaction.word);
    while (_entryHistory.Count > _wordData.Length) {
      _entryHistory.RemoveAt(_entryHistory.Count - 1);
    }

    WordData wordData = _wordData.Single(d => d.Word == _currentTransaction.word);
    wordData.Turns.Add(turnData);

    _currentTransaction = null;
  }

  /// <summary>
  /// Updates this bank to match the given word list.  This bank will be updated so
  /// that it contains all of the words in the given word list, and will delete any
  /// words not on the given word list.
  /// 
  /// This method returns true if any modifications were actually made, and false otherwise.
  /// </summary>
  public bool MatchToWordList(IEnumerable<string> wordList) {
    if (new HashSet<string>(wordList).SetEquals(_wordData.Select(d => d.Word))) {
      return false;
    }

    //Remove all words that are not in the word list
    _wordData = _wordData.Where(d => wordList.Contains(d.Word)).ToArray();
    _entryHistory = _entryHistory.Where(w => wordList.Contains(w)).ToList();

    //Words to add are all legal words except the ones already in this word bank
    var wordsToAdd = wordList.Except(_wordData.Select(d => d.Word));
    _wordData = _wordData.Concat(wordsToAdd.Select(w => new WordData(w))).ToArray();

    return true;
  }

  [Serializable]
  public class WordData : IEquatable<WordData> {
    /// <summary>
    /// The actual word itself
    /// </summary>
    public string Word;

    /// <summary>
    /// Every turn where this word came up as an option
    /// </summary>
    public List<TurnData> Turns = new List<TurnData>();

    public WordData(string word) {
      Word = word;
    }

    [Serializable]
    public class TurnData {

      /// <summary>
      /// Whether or not the word was rejected during this turn
      /// </summary>
      public bool WasRejected;

      /// <summary>
      /// A normalized time it took for someone to reject the word.  0 is no time at all, 1
      /// is a full turn.
      /// </summary>
      public float RejectTime;

      /// <summary>
      /// The total number of players playing during this turn
      /// </summary>
      public int TotalPlayers;

      /// <summary>
      /// A normalized collection of times it took people to guess the word.  0 is no time at all,
      /// 1 is a full turn.  This array only contains values for actual guesses.
      /// </summary>
      public float[] GuessTimes;

      /// <summary>
      /// A total number of players that successfully guessed this turn
      /// </summary>
      public int SuccessPlayers {
        get {
          return GuessTimes.Length;
        }
      }

      /// <summary>
      /// The total number of players that failed to guess correctly this turn
      /// </summary>
      public int FailPlayers {
        get {
          return TotalPlayers - SuccessPlayers;
        }
      }
    }

    /// <summary>
    /// The total number of turns where this word came up as an option
    /// </summary>
    public int TotalTurnsSeen {
      get {
        return Turns.Count;
      }
    }

    /// <summary>
    /// The total number of turns where this word was rejected instead of being played
    /// </summary>
    public int TotalRejections {
      get {
        return Turns.Count(s => s.WasRejected);
      }
    }

    /// <summary>
    /// Total number of games this word was used in where it was not rejected.
    /// </summary>
    public int TotalTurnsPlayed {
      get {
        return TotalTurnsSeen - TotalRejections;
      }
    }

    /// <summary>
    /// Total number of games where this word was guessed successfully
    /// </summary>
    public int TotalSuccessTurns {
      get {
        return Turns.Count(s => s.GuessTimes.Length > 0);
      }
    }

    /// <summary>
    /// Total number of games played where the word was never guessed.  Does
    /// not count games where the word was rejected.
    /// </summary>
    public int TotalFailTurns {
      get {
        return TotalTurnsPlayed - TotalSuccessTurns;
      }
    }

    public float Difficulty {
      get {
        //The basic ratio difficulty.  How many games was there at least 1 success, vs how many games
        //were there no successes at all.  Rejections makes this ratio go up!  If the number of games
        //is less than 3, the game assumes that there have been 1 success game for every game missing.
        float successFailDifficulty;
        float successFailWeight;
        {
          int mockSuccess = TotalSuccessTurns + Mathf.Max(0, 3 - TotalTurnsSeen);

          successFailWeight = 1;
          successFailDifficulty = 1.0f - successFailWeight * mockSuccess / Mathf.Min(3, TotalTurnsSeen);
        }

        //The failure ratio difficulty.  This difficulty gets higher the fewer people can guess a word
        //during a turn.  The weight starts at 0 and increases to max after 3 turns.
        float failureRatioDifficulty = 0;
        float failureRatioWeight = 0;
        if (TotalTurnsPlayed > 0) {
          failureRatioWeight = Mathf.Lerp(Mathf.InverseLerp(1, 3, TotalTurnsPlayed), 0.5f, 1.0f);
          failureRatioDifficulty = Turns.Where(t => !t.WasRejected).Select(t => t.FailPlayers / (float)t.TotalPlayers).Average();
        }

        //The failure time difficulty.  This difficulty gets higher the longer it takes to guess a word.
        //The weight starts at 0 and increases to max after 3 turns.
        float guessSpeedDifficulty = 0;
        float guessSpeedWeight = 0;
        if (TotalTurnsPlayed > 0) {
          guessSpeedWeight = Mathf.Lerp(Mathf.InverseLerp(1, 3, TotalTurnsPlayed), 0.25f, 0.5f);
          guessSpeedDifficulty = Turns.Where(t => !t.WasRejected).Select(t => t.GuessTimes.Min()).Average();
        }

        return (successFailDifficulty + failureRatioDifficulty + guessSpeedDifficulty) /
               (successFailWeight + failureRatioWeight + guessSpeedWeight);
      }
    }

    public override bool Equals(object obj) {
      return base.Equals(obj);
    }

    public bool Equals(WordData word) {
      return Word == word.Word;
    }

    public override int GetHashCode() {
      return Word.GetHashCode();
    }
  }
}
