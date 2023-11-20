using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.Serialization;
using static RichTextUtility;

public struct Message : INetworkSerializable {
    public ulong netId;
    public float boardDisplayTime;
    public float timeLeft;
    public string text;
    public Color32 color;
    public bool bold;

    public static Message Server(string text) {
        return new Message() {
            netId = 256888,
            boardDisplayTime = 0,
            text = text,
            color = new Color(0.7f, 0, 0, 1),
            bold = true
        };
    }

    public static Message User(string text) {
        return new Message() {
            netId = Player.Local.NetworkObjectId,
            boardDisplayTime = GameCoordinator.instance.DrawingBoard.BoardDisplayTime,
            timeLeft = GameCoordinator.instance.TimeLeft,
            text = text,
            color = new Color(0, 0, 0, 1),
            bold = false
        };
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        serializer.SerializeValue(ref netId);
        serializer.SerializeValue(ref boardDisplayTime);
        serializer.SerializeValue(ref timeLeft);
        serializer.SerializeValue(ref text);
        serializer.SerializeValue(ref color);
        serializer.SerializeValue(ref bold);
    }
}

public class MessageBoard : NetworkBehaviour {
    public const int MAX_MESSAGE_HISTORY = 100;

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

    public override void OnNetworkSpawn() {
        base.OnNetworkSpawn();

        _messageList.Clear();
        PushText();
    }

    private void PushText() {
        string text = "";
        foreach (var item in _messageList) {
            if (text != "") {
                text += "\n";
            }
            text += item;
        }
        _textBox.text = text;
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
    public void SubmitMessageClientRpc(Message msg, ClientRpcParams clientRpcParams = default) {
        LocalSubmitMessage(msg);
    }

    public void LocalSubmitMessage(Message msg) {
        string text = msg.text;

        var player = Player.All.SingleOrDefault(p => p.NetworkObjectId == msg.netId);
        if (player != null) {
            text = B(player.GameName.Value.Value) + ": " + text;
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

        PushText();

        StartCoroutine(updateAfterOneFrame());
    }

    private IEnumerator updateAfterOneFrame() {
        yield return null;
        Canvas.ForceUpdateCanvases();
        _scrollRect.verticalNormalizedPosition = 0;
    }
}
