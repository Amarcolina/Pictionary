using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.Serialization;

public struct Message {
  public NetworkInstanceId netId;
  public float boardDisplayTime;
  public float timeLeft;
  public string text;
  public Color32 color;
  public bool bold;

  public static Message Server(string text) {
    return new Message() {
      netId = NetworkInstanceId.Invalid,
      boardDisplayTime = 0,
      text = text,
      color = new Color(0.7f, 0, 0, 1),
      bold = true
    };
  }

  public static Message User(string text) {
    return new Message() {
      netId = Player.Local.netId,
      boardDisplayTime = GameCoordinator.instance.DrawingBoard.BoardDisplayTime,
      timeLeft = GameCoordinator.instance.TimeLeft,
      text = text,
      color = new Color(0, 0, 0, 1),
      bold = false
    };
  }
}

public class MessageBoard : NetworkBehaviour {
  public const int MAX_MESSAGE_HISTORY = 48;

  [SerializeField]
  [FormerlySerializedAs("textBox")]
  private Text _textBox;

  [SerializeField]
  [FormerlySerializedAs("inputField")]
  private InputField _inputField;

  [SerializeField]
  [FormerlySerializedAs("scrollRect")]
  private ScrollRect _scrollRect;

  [SerializeField]
  [FormerlySerializedAs("messageList")]
  private List<string> _messageList;

  private void Update() {
    string text = "";
    foreach (var item in _messageList) {
      if (text != "") {
        text += "\n";
      }
      text += item;
    }
    _textBox.text = text;

    if (Input.GetKey(KeyCode.F3)) {
      OnSubmitText(Random.Range(0, 1000).ToString().PadLeft(4, '0'));
    }
  }

  public void OnSubmitText(string text) {
    _inputField.ActivateInputField();
    if (string.IsNullOrEmpty(text)) {
      return;
    }
    if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.F3)) {
      Player.Local.ExecuteClientMessage(Message.User(text));
      _inputField.text = "";
    }
  }

  [ClientRpc]
  public void RpcSubmitMessage(Message msg) {
    LocalSubmitMessage(msg);
  }

  [TargetRpc]
  public void TargetSubmitMessage(NetworkConnection conn, Message msg) {
    LocalSubmitMessage(msg);
  }

  [Client]
  public void LocalSubmitMessage(Message msg) {
    string text = msg.text;

    if (msg.netId != NetworkInstanceId.Invalid) {
      text = Player.All.Single(p => p.netId == msg.netId).GameName + ": " + text;
    }

    msg.color.a = 255;
    text = "<color=#" + ColorUtility.ToHtmlStringRGB(msg.color) + ">" + text + "</color>";
    if (msg.bold) {
      text = "<b>" + text + "</b>";
    }

    _messageList.Add(text);
    while (_messageList.Count > MAX_MESSAGE_HISTORY) {
      _messageList.RemoveAt(0);
    }

    StartCoroutine(updateAfterOneFrame());
  }

  private IEnumerator updateAfterOneFrame() {
    yield return null;
    Canvas.ForceUpdateCanvases();
    _scrollRect.verticalNormalizedPosition = 0;
  }
}
