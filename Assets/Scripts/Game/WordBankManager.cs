using System;
using System.IO;
using UnityEngine;

[CreateAssetMenu(menuName = "Other/WordBankManager", order = 90000)]
public class WordBankManager : ScriptableObject {
  private const string BANK_FILENAME = "WordBank.json";

  [SerializeField]
  private TextAsset defaultWordList;

  [NonSerialized]
  private WordBank _activeWordBank;

  public static string BankPath {
    get {
#if UNITY_EDITOR
      return Path.Combine(Application.dataPath, Path.Combine("..", BANK_FILENAME));
#else
      return Path.Combine(Application.persistentDataPath, BANK_FILENAME);
#endif
    }
  }

  public WordBank Bank {
    get {
      if (_activeWordBank == null) {
        var defaultWords = defaultWordList.text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        if (File.Exists(BankPath)) {
          try {
            _activeWordBank = JsonUtility.FromJson<WordBank>(File.ReadAllText(BankPath));
            _activeWordBank.MatchToWordList(defaultWords);
          } catch (Exception e) {
            Debug.LogWarning("Could not load word bank for unknown reason.");
            Debug.LogException(e);
          }
        }

        if (_activeWordBank == null) {
          _activeWordBank = new WordBank(defaultWords);
        }
      }

      return _activeWordBank;
    }
  }

  public void SaveActiveWordBank() {
    if (_activeWordBank != null) {
      File.WriteAllText(BankPath, JsonUtility.ToJson(_activeWordBank, prettyPrint: true));
    }
  }
}
