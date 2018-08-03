using System;
using System.Linq;
using UnityEngine;

public class BasicSelector : IWordSelector {

  public float PercentToIgnore = 0.75f;

  public string SelectWord(WordBank.WordData[] data, string[] entryHistory) {
    if (data.Length == 0) {
      throw new InvalidOperationException("Cannot select a word if there are no words to choose from.");
    }

    var wordsToIgnore = entryHistory.Take(Mathf.RoundToInt(data.Length * PercentToIgnore));
    var wordsToTake = data.Select(d => d.Word).Except(wordsToIgnore).ToArray();

    if (wordsToTake.Length == 0) {
      wordsToTake = data.Select(d => d.Word).ToArray();
    }

    return wordsToTake[UnityEngine.Random.Range(0, wordsToTake.Length)];
  }
}

