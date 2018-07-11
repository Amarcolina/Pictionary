using System.Collections.Generic;
using UnityEngine;

public static class WordUtility {

  public static bool IsGuessClose(string guess, string actual) {
    guess = sanitize(guess);
    actual = sanitize(actual);

    if (actual.Length <= 4) {
      return false;
    }

    int dist = editDistance(guess, actual);
    if (dist <= 8) {
      return dist <= 1;
    } else {
      return dist <= 2;
    }
  }

  public static bool DoesGuessMatch(string guess, string actual) {
    return sanitize(guess) == sanitize(actual);
  }

  private static int editDistance(string s, string t) {
    int n = s.Length;
    int m = t.Length;
    int[,] d = new int[n + 1, m + 1];

    if (n == 0) {
      return m;
    }

    if (m == 0) {
      return n;
    }

    for (int i = 0; i <= n; d[i, 0] = i++) { }
    for (int j = 0; j <= m; d[0, j] = j++) { }

    for (int i = 1; i <= n; i++) {
      for (int j = 1; j <= m; j++) {
        int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
        d[i, j] = Mathf.Min(d[i - 1, j] + 1, d[i, j - 1] + 1, d[i - 1, j - 1] + cost);
      }
    }

    return d[n, m];
  }

  private static List<char> _tmpChars = new List<char>();
  private static string sanitize(string word) {
    for (int i = 0; i < word.Length; i++) {
      var c = char.ToLower(word[i]);

      if (char.IsLetterOrDigit(c)) {
        _tmpChars.Add(c);
      }
    }

    //If word ends in an s, remove it
    if (_tmpChars.Count > 0 && _tmpChars[_tmpChars.Count - 1] == 's') {
      _tmpChars.RemoveAt(_tmpChars.Count - 1);
    }

    string result = new string(_tmpChars.ToArray());
    _tmpChars.Clear();
    return result;
  }
}
