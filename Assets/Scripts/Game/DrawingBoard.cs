using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using Unity.Netcode;
using UnityEngine.Serialization;

public class DrawingBoard : NetworkBehaviour {
    public const float SEND_INTERVAL = 1.0f / 20.0f;
    public const float INTERP_BUFFER = 1.0f / 40.0f;

    [SerializeField]
    [FormerlySerializedAs("resolutionX")]
    private int _resolutionX;
    public int ResolutionX {
        get { return _resolutionX; }
    }

    [SerializeField]
    [FormerlySerializedAs("resolutionY")]
    private int _resolutionY;
    public int ResolutionY {
        get { return _resolutionY; }
    }

    [SerializeField]
    [FormerlySerializedAs("maxActionsPerFrame")]
    private int _maxActionsPerFrame = 20;

    [SerializeField]
    [FormerlySerializedAs("boardImage")]
    private Image _boardImage;

    [SerializeField]
    [FormerlySerializedAs("previewImagePrefab")]
    private Image _previewImagePrefab;

    private DrawableCanvas _boardCanvas;

    private Dictionary<ulong, Image> _previewImages = new Dictionary<ulong, Image>();
    private Dictionary<ulong, DrawableCanvas> _previewCanvases = new Dictionary<ulong, DrawableCanvas>();

    private float _recieveTime;
    private float _firstRecieveTimestamp;
    private float _lastSendTime;
    private Queue<BrushAction> _toSendQueue = new Queue<BrushAction>();

    private Queue<BrushAction> _toDrawQueue = new Queue<BrushAction>();
    private Dictionary<ulong, Queue<BrushAction>> _toDrawQueues = new Dictionary<ulong, Queue<BrushAction>>();

    private float _latestBrushTimestamp;
    private float _latestBrushDisplayTime;

    public float BoardDisplayTime {
        get {
            return (GameCoordinator.instance.GameTime - _latestBrushDisplayTime) + _latestBrushTimestamp;
        }
    }

    public DrawableCanvas Canvas {
        get {
            return _boardCanvas;
        }
    }

    private void Awake() {
        _boardCanvas = new DrawableCanvas(_resolutionX, _resolutionY);

        var sprite = Sprite.Create(_boardCanvas.Texture, new Rect(0, 0, _resolutionX, _resolutionY), Vector2.zero);
        _boardImage.sprite = sprite;
    }

    public override void OnNetworkDespawn() {
        base.OnNetworkDespawn();

        _boardCanvas.Dispose();

        foreach (var canvas in _previewCanvases.Values) {
            canvas.Dispose();
        }
    }

    private Image getPreviewImage(ulong player) {
        Image image;
        if (!_previewImages.TryGetValue(player, out image)) {
            image = Instantiate(_previewImagePrefab);

            image.transform.SetParent(transform.parent, worldPositionStays: true);
            image.transform.localPosition = _previewImagePrefab.transform.localPosition;

            image.gameObject.SetActive(true);
            _previewImages[player] = image;
        }

        return image;
    }

    private DrawableCanvas getPreviewCanvas(ulong player) {
        DrawableCanvas canvas;
        if (!_previewCanvases.TryGetValue(player, out canvas)) {
            canvas = new DrawableCanvas(_resolutionX, _resolutionY);
            _previewCanvases[player] = canvas;

            var sprite = Sprite.Create(canvas.Texture, new Rect(0, 0, _resolutionX, _resolutionY), Vector2.zero);
            getPreviewImage(player).sprite = sprite;
        }

        return canvas;
    }

    public void ApplyBrushAction(BrushAction action) {
        action.time = GameCoordinator.instance.GameTime;
        //SetDirtyBit(1);

        //We add the brush action to the send queue so that
        //when we serialize this board, the actions get serialized
        //and sent out
        _toSendQueue.Enqueue(action);

        //Serialized stuff never gets sent to the server (it's the
        //thing being serialized) so we also want to add the action
        //to the servers toDraw queue so that it gets drawn
        _toDrawQueue.Enqueue(action);
    }

    public void ClearAndReset() {
        var clearAction = new BrushAction() {
            type = BrushActionType.Clear,
            drawerId = Player.Local.NetworkObjectId
        };

        //We apply a brush action to clear all clients
        //and predict a brush action to clear the server
        ApplyBrushAction(clearAction);
        PredictBrushAction(clearAction);

        //We also send preview actions to everybody so that 
        //things get cleared out
        foreach (var player in Player.All) {
            ApplyBrushAction(new BrushAction() {
                type = BrushActionType.Line,
                position0 = new Vector2Int(-100, -100),
                position1 = new Vector2Int(-100, -100),
                size = 0,
                drawerId = player.NetworkObjectId,
                isPreview = true
            });
        }
    }

    public void PredictBrushAction(BrushAction action) {
        Assert.AreEqual(action.drawerId, Player.Local.NetworkObjectId);
        drawBrushActionToCanvases(action);
    }

    private void Update() {
        if (Player.Local == null) {
            return;
        }

        while (_toDrawQueue.Count > 0) {
            var action = _toDrawQueue.Peek();

            //Skip actions that are from ourselves because those are predicted
            if (action.drawerId == Player.Local.NetworkObjectId) {
                _toDrawQueue.Dequeue();
                continue;
            }

            if ((action.time + SEND_INTERVAL + INTERP_BUFFER) < GameCoordinator.instance.GameTime) {
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

            if (player == Player.Local.NetworkObjectId) {
                continue;
            }

            BrushAction? lastAction = null;

            int actionsTaken = 0;
            while (queue.Count > 0) {
                var action = queue.Dequeue();
                lastAction = action;

                if (!action.isPreview) {
                    _latestBrushTimestamp = Mathf.Max(_latestBrushTimestamp, action.time);
                    _latestBrushDisplayTime = Mathf.Max(_latestBrushDisplayTime, GameCoordinator.instance.GameTime);

                    _boardCanvas.ApplyBrushAction(action);
                    actionsTaken++;

                    if (actionsTaken >= _maxActionsPerFrame) {
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

        //Sometimes the serialization does not send all actions
        //this can happen if there are too many actions to serialize
        //so we check here every frame and set the dirty bit if there
        //are some left
        //if (_toSendQueue.Count > 0) {
        //    SetDirtyBit(1);
        //}

        if ((Time.time - _lastSendTime) > SEND_INTERVAL) {
            _lastSendTime = Time.time;

            for (int i = 0; i < _maxActionsPerFrame; i++) {
                if (_toSendQueue.Count == 0) {
                    break;
                }

                var action = _toSendQueue.Dequeue();

                if (IsServer) {
                    SendActionToClientRpc(action);
                } else {
                    SendActionToServerRpc(action);
                }
            }
        }
    }

    [ServerRpc]
    private void SendActionToServerRpc(BrushAction action) {
        SendActionToClientRpc(action);
    }

    [ClientRpc]
    private void SendActionToClientRpc(BrushAction action) {
        //Ignore actions that came from ourselves
        if (action.drawerId == Player.Local.NetworkObjectId) {
            return;
        }

        _toDrawQueue.Enqueue(action);
    }

    //public override bool OnSerialize(NetworkWriter writer, bool initialState) {
    //    if (_toSendQueue.Count > 0) {
    //        serializeBrushActions(writer);
    //        _toSendQueue.Clear();
    //        return true;
    //    } else {
    //        return false;
    //    }
    //}

    //public override void OnDeserialize(NetworkReader reader, bool initialState) {
    //    deserializeBrushActions(reader);
    //}

    private void drawBrushActionToCanvases(BrushAction action) {
        var previewCanvas = getPreviewCanvas(action.drawerId);
        previewCanvas.Clear(new Color32(0, 0, 0, 0));
        if (action.isPreview) {
            previewCanvas.ApplyBrushAction(action);
        } else {
            _boardCanvas.ApplyBrushAction(action);
        }
    }

    //private void serializeBrushActions(NetworkWriter writer) {
    //    int toSend = Mathf.Min(_maxActionsPerFrame, _toSendQueue.Count);

    //    writer.WritePackedUInt32((uint)toSend);

    //    while (toSend != 0) {
    //        var action = _toSendQueue.Dequeue();
    //        action.Serialize(writer);
    //        toSend--;
    //    }
    //}

    //private void deserializeBrushActions(NetworkReader reader) {
    //    int count = (int)reader.ReadPackedUInt32();
    //    for (int i = 0; i < count; i++) {
    //        BrushAction action = new BrushAction();
    //        action.Deserialize(reader);
    //        _toDrawQueue.Enqueue(action);
    //    }
    //}

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
