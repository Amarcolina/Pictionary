using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using UnityEngine.Networking;

[NetworkSettings(channel = 2, sendInterval = SEND_INTERVAL)]
public class DrawingBoard : NetworkBehaviour {
  public const float SEND_INTERVAL = 1.0f / 20.0f;
  public const float INTERP_BUFFER = 1.0f / 40.0f;

  public int resolutionX, resolutionY;
  public int maxActionsPerFrame = 20;
  public Image boardImage;
  public Image previewImagePrefab;

  private DrawableCanvas _boardCanvas;

  private Dictionary<uint, Image> _previewImages = new Dictionary<uint, Image>();
  private Dictionary<uint, DrawableCanvas> _previewCanvases = new Dictionary<uint, DrawableCanvas>();

  private float _recieveTime;
  private float _firstRecieveTimestamp;
  private Queue<BrushAction> _toSendQueue = new Queue<BrushAction>();

  private Queue<BrushAction> _toDrawQueue = new Queue<BrushAction>();
  private Dictionary<NetworkInstanceId, Queue<BrushAction>> _toDrawQueues = new Dictionary<NetworkInstanceId, Queue<BrushAction>>();

  private float _latestBrushTimestamp;
  private float _latestBrushDisplayTime;

  public float boardDisplayTime {
    get {
      return (GameCoordinator.instance.gameTime - _latestBrushDisplayTime) + _latestBrushTimestamp;
    }
  }

  public DrawableCanvas canvas {
    get {
      return _boardCanvas;
    }
  }

  private void Awake() {
    _boardCanvas = new DrawableCanvas(resolutionX, resolutionY);

    var sprite = Sprite.Create(_boardCanvas.texture, new Rect(0, 0, resolutionX, resolutionY), Vector2.zero);
    boardImage.sprite = sprite;
  }

  private void OnDestroy() {
    _boardCanvas.Dispose();

    foreach (var canvas in _previewCanvases.Values) {
      canvas.Dispose();
    }
  }

  private Image getPreviewImage(NetworkInstanceId player) {
    Image image;
    if (!_previewImages.TryGetValue(player.Value, out image)) {
      image = Instantiate(previewImagePrefab);

      image.transform.SetParent(transform.parent, worldPositionStays: true);
      image.transform.localPosition = previewImagePrefab.transform.localPosition;

      image.gameObject.SetActive(true);
      _previewImages[player.Value] = image;
    }

    return image;
  }

  private DrawableCanvas getPreviewCanvas(NetworkInstanceId player) {
    DrawableCanvas canvas;
    if (!_previewCanvases.TryGetValue(player.Value, out canvas)) {
      canvas = new DrawableCanvas(resolutionX, resolutionY);
      _previewCanvases[player.Value] = canvas;

      var sprite = Sprite.Create(canvas.texture, new Rect(0, 0, resolutionX, resolutionY), Vector2.zero);
      getPreviewImage(player).sprite = sprite;
    }

    return canvas;
  }

  [Server]
  public void ApplyBrushAction(BrushAction action) {
    action.time = Time.realtimeSinceStartup;
    _toSendQueue.Enqueue(action);
    SetDirtyBit(1);

    if (isServer) {
      _toDrawQueue.Enqueue(action);
    }
  }

  [Server]
  public void ClearAndReset() {
    ApplyBrushAction(new BrushAction() {
      type = BrushActionType.Clear,
      drawerId = Player.local.netId
    });

    foreach (var player in Player.all) {
      ApplyBrushAction(new BrushAction() {
        type = BrushActionType.Line,
        position0 = new Vector2Int(-100, -100),
        position1 = new Vector2Int(-100, -100),
        size = 0,
        drawerId = player.netId,
        isPreview = true
      });
    }
  }

  [Client]
  public void PredictBrushAction(BrushAction action) {
    Assert.AreEqual(action.drawerId, Player.local.netId);
    drawBrushActionToCanvases(action);
  }

  [ClientCallback]
  private void Update() {
    while (_toDrawQueue.Count > 0) {
      var action = _toDrawQueue.Peek();

      //Skip actions that are from ourselves because those are predicted
      if (action.drawerId == Player.local.netId) {
        _toDrawQueue.Dequeue();
        continue;
      }

      if ((action.time + SEND_INTERVAL + INTERP_BUFFER) < GameCoordinator.instance.gameTime) {
        Queue<BrushAction> queue;
        if (!_toDrawQueues.TryGetValue(action.drawerId, out queue)) {
          queue = new Queue<BrushAction>();
          _toDrawQueues[action.drawerId] = queue;
        }

        queue.Enqueue(action);
        _toDrawQueue.Dequeue();
      } else {
        break;
      }
    }

    foreach (var pair in _toDrawQueues) {
      var player = pair.Key;
      var queue = pair.Value;

      if (player == Player.local.netId) {
        continue;
      }

      BrushAction? lastAction = null;

      int actionsTaken = 0;
      while (queue.Count > 0) {
        var action = queue.Dequeue();
        lastAction = action;

        if (!action.isPreview) {
          _latestBrushTimestamp = Mathf.Max(_latestBrushTimestamp, action.time);
          _latestBrushDisplayTime = Mathf.Max(_latestBrushDisplayTime, GameCoordinator.instance.gameTime);

          _boardCanvas.ApplyBrushAction(action);
          actionsTaken++;

          if (actionsTaken >= maxActionsPerFrame) {
            break;
          }
        }
      }

      if (lastAction.HasValue && lastAction.Value.isPreview) {
        getPreviewCanvas(player).Clear(new Color32(0, 0, 0, 0));
        getPreviewCanvas(player).ApplyBrushAction(lastAction.Value);
      } else if (actionsTaken > 0) {
        getPreviewCanvas(player).Clear(new Color32(0, 0, 0, 0));
      }
    }

    _boardCanvas.Update();
  }

  public override bool OnSerialize(NetworkWriter writer, bool initialState) {
    if (_toSendQueue.Count > 0) {
      serializeBrushActions(writer);
      _toSendQueue.Clear();
      return true;
    } else {
      return false;
    }
  }

  public override void OnDeserialize(NetworkReader reader, bool initialState) {
    deserializeBrushActions(reader);
  }

  private void drawBrushActionToCanvases(BrushAction action) {
    var previewCanvas = getPreviewCanvas(action.drawerId);
    previewCanvas.Clear(new Color32(0, 0, 0, 0));
    if (action.isPreview) {
      previewCanvas.ApplyBrushAction(action);
    } else {
      _boardCanvas.ApplyBrushAction(action);
    }
  }

  private void serializeBrushActions(NetworkWriter writer) {
    int toSend = Mathf.Min(maxActionsPerFrame, _toSendQueue.Count);

    writer.WritePackedUInt32((uint)toSend);

    while (toSend != 0) {
      var action = _toSendQueue.Dequeue();
      action.Serialize(writer);
      toSend--;
    }
  }

  private void deserializeBrushActions(NetworkReader reader) {
    int count = (int)reader.ReadPackedUInt32();
    for (int i = 0; i < count; i++) {
      BrushAction action = new BrushAction();
      action.Deserialize(reader);
      _toDrawQueue.Enqueue(action);
    }
  }

  private struct ColorKey : IEquatable<ColorKey> {
    public byte r, g, b, a;

    public static implicit operator ColorKey(Color32 color) {
      ColorKey key;
      key.r = color.r;
      key.g = color.g;
      key.b = color.b;
      key.a = color.a;
      return key;
    }

    public static implicit operator Color32(ColorKey key) {
      return new Color32() {
        r = key.r,
        g = key.g,
        b = key.b,
        a = key.a
      };
    }

    public override int GetHashCode() {
      int hash = r;
      hash = hash * 3 + g;
      hash = hash * 3 + b;
      hash = hash * 3 + a;
      return hash;
    }

    public bool Equals(ColorKey other) {
      return r == other.r &&
             b == other.b &&
             g == other.g &&
             a == other.a;
    }
  }
}
