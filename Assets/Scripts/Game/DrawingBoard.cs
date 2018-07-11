using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using UnityEngine.Networking;
using UnityEngine.Profiling;
using Unity.Collections;

[NetworkSettings(channel = 2, sendInterval = SEND_INTERVAL)]
public class DrawingBoard : NetworkBehaviour {
  public const float SEND_INTERVAL = 1.0f / 20.0f;
  public const float INTERP_BUFFER = 1.0f / 40.0f;

  public int resolutionX, resolutionY;
  public Image image;

  private DrawableCanvas _canvas;

  private float _recieveTime;
  private float _firstRecieveTimestamp;
  private Queue<BrushAction> _toSendQueue = new Queue<BrushAction>();
  private Queue<BrushAction> _toDrawQueue = new Queue<BrushAction>();

  private void Awake() {
    _canvas = new DrawableCanvas(resolutionX, resolutionY);

    var sprite = Sprite.Create(_canvas.texture, new Rect(0, 0, resolutionX, resolutionY), Vector2.zero);
    image.sprite = sprite;
  }

  private void OnDestroy() {
    _canvas.Dispose();
  }

  [Server]
  public void ApplyBrushAction(BrushAction action) {
    action.time = Time.realtimeSinceStartup;
    _toSendQueue.Enqueue(action);
    SetDirtyBit(1);

    if (isServer) {
      _canvas.ApplyBrushAction(action);
    }
  }

  [Client]
  public void PredictBrushAction(BrushAction action) {
    Assert.AreEqual(action.drawerId, Player.local.netId);
    _canvas.ApplyBrushAction(action);
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
        _canvas.ApplyBrushAction(action);
        _toDrawQueue.Dequeue();
      } else {
        break;
      }
    }
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

  private void serializeBrushActions(NetworkWriter writer) {
    writer.WritePackedUInt32((uint)_toSendQueue.Count);
    while (_toSendQueue.Count > 0) {
      var action = _toSendQueue.Dequeue();

      writer.Write(action.drawerId);
      writer.Write((byte)action.type);
      writer.Write(action.time);

      switch (action.type) {
        case BrushActionType.Clear:
          break;
        case BrushActionType.Line:
        case BrushActionType.Box:
        case BrushActionType.Oval:
          writer.Write((ushort)action.position0.x);
          writer.Write((ushort)action.position0.y);
          writer.Write((ushort)action.position1.x);
          writer.Write((ushort)action.position1.y);
          writer.Write(action.color);
          writer.Write((byte)action.size);
          break;
        case BrushActionType.FloodFill:
          writer.Write((ushort)action.position0.x);
          writer.Write((ushort)action.position0.y);
          writer.Write(action.color);
          break;
      }
    }
  }

  private void deserializeBrushActions(NetworkReader reader) {
    int count = (int)reader.ReadPackedUInt32();
    for (int i = 0; i < count; i++) {
      BrushAction action = new BrushAction();

      action.drawerId = reader.ReadNetworkId();
      action.type = (BrushActionType)reader.ReadByte();
      action.time = reader.ReadSingle();

      switch (action.type) {
        case BrushActionType.Clear:
          break;
        case BrushActionType.Line:
        case BrushActionType.Box:
        case BrushActionType.Oval:
          action.x0 = (short)reader.ReadUInt16();
          action.y0 = (short)reader.ReadUInt16();
          action.x1 = (short)reader.ReadUInt16();
          action.y1 = (short)reader.ReadUInt16();
          action.color = reader.ReadColor32();
          action.size = (byte)reader.ReadByte();
          break;
        case BrushActionType.FloodFill:
          action.x0 = (short)reader.ReadUInt16();
          action.y0 = (short)reader.ReadUInt16();
          action.color = reader.ReadColor32();
          break;
      }

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
