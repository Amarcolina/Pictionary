using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

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
      netId = Player.local.netId,
      boardDisplayTime = GameCoordinator.instance.drawingBoard.boardDisplayTime,
      timeLeft = GameCoordinator.instance.timeLeft,
      text = text,
      color = new Color(0, 0, 0, 1),
      bold = false
    };
  }
}

public class MessageBoard : NetworkBehaviour {
  public const int MAX_MESSAGE_HISTORY = 128;

  public Text textBox;
  public InputField inputField;
  public ScrollRect scrollRect;
  public List<string> messageList;

  [ClientCallback]
  private void Update() {
    string text = "";
    foreach (var item in messageList) {
      if (text != "") {
        text += "\n";
      }
      text += item;
    }
    textBox.text = text;

    if (Input.GetKey(KeyCode.F3)) {
      OnSubmitText(Random.Range(0, 1000).ToString().PadLeft(4, '0'));
    }
  }

  public void OnSubmitText(string text) {
    inputField.ActivateInputField();
    if (string.IsNullOrEmpty(text)) {
      return;
    }
    if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.F3)) {
      Player.local.CmdAddMessage(Message.User(text));
      inputField.text = "";
    }
  }

  [ClientRpc]
  public void RpcSubmitMessage(Message msg) {
    submitMessage(msg);
  }

  [TargetRpc]
  public void TargetSubmitMessage(NetworkConnection conn, Message msg) {
    submitMessage(msg);
  }

  [Client]
  private void submitMessage(Message msg) {
    string text = msg.text;

    if (msg.netId != NetworkInstanceId.Invalid) {
      text = Player.all.Single(p => p.netId == msg.netId).gameName + ": " + text;
    }

    msg.color.a = 255;
    text = "<color=#" + ColorUtility.ToHtmlStringRGB(msg.color) + ">" + text + "</color>";
    if (msg.bold) {
      text = "<b>" + text + "</b>";
    }

    messageList.Add(text);
    while (messageList.Count > MAX_MESSAGE_HISTORY) {
      messageList.RemoveAt(0);
    }

    StartCoroutine(updateAfterOneFrame());
  }

  private IEnumerator updateAfterOneFrame() {
    yield return null;
    Canvas.ForceUpdateCanvases();
    scrollRect.verticalNormalizedPosition = 0;
  }
}
