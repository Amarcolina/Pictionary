using System;
using System.Collections;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.Serialization;

public struct BrushAction : INetworkSerializable {

    public ulong drawerId {
        get => _drawerId;
        set => _drawerId = value;
    }

    public BrushActionType type {
        get => (BrushActionType)_type;
        set => _type = (byte)value;
    }

    public int x0 {
        get => _x0;
        set => _x0 = (short)value;
    }

    public int y0 {
        get => _y0;
        set => _y0 = (short)value;
    }

    public int x1 {
        get => _x1;
        set => _x1 = (short)value;
    }

    public int y1 {
        get => _y1;
        set => _y1 = (short)value;
    }

    public Color32 color {
        get => new Color32(_r, _g, _b, 255);
        set {
            _r = value.r;
            _g = value.g;
            _b = value.b;
        }
    }

    public int size {
        get => _size;
        set => _size = (byte)value;
    }

    public float time {
        get => _time;
        set => _time = value;
    }

    public bool isPreview {
        get => _isPreview;
        set => _isPreview = value;
    }

    private ulong _drawerId;
    private byte _r, _g, _b;
    private byte _size;
    private byte _type;
    private bool _isPreview;
    private short _x0, _y0;
    private short _x1, _y1;
    private float _time;

    public Vector2Int position0 {
        get {
            return new Vector2Int(x0, y0);
        }
        set {
            x0 = value.x;
            y0 = value.y;
        }
    }

    public Vector2Int position1 {
        get {
            return new Vector2Int(x1, y1);
        }
        set {
            x1 = value.x;
            y1 = value.y;
        }
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
        serializer.SerializeValue(ref _drawerId);
        serializer.SerializeValue(ref _type);
        serializer.SerializeValue(ref _time);
        serializer.SerializeValue(ref _isPreview);

        switch (type) {
            case BrushActionType.Clear:
                break;
            case BrushActionType.Line:
            case BrushActionType.Box:
            case BrushActionType.Oval:
            case BrushActionType.PreviewBox:
                serializer.SerializeValue(ref _x0);
                serializer.SerializeValue(ref _y0);
                serializer.SerializeValue(ref _x1);
                serializer.SerializeValue(ref _y1);

                serializer.SerializeValue(ref _r);
                serializer.SerializeValue(ref _g);
                serializer.SerializeValue(ref _b);
                serializer.SerializeValue(ref _size);
                break;
            case BrushActionType.FloodFill:
                serializer.SerializeValue(ref _x0);
                serializer.SerializeValue(ref _y0);

                serializer.SerializeValue(ref _r);
                serializer.SerializeValue(ref _g);
                serializer.SerializeValue(ref _b);
                break;
            case BrushActionType.Erase:
                serializer.SerializeValue(ref _x0);
                serializer.SerializeValue(ref _y0);
                serializer.SerializeValue(ref _x1);
                serializer.SerializeValue(ref _y1);

                serializer.SerializeValue(ref _size);
                break;
        }
    }
}

public enum BrushActionType {
    Clear = 0,
    Line = 1,
    Box = 2,
    Oval = 3,
    FloodFill = 4,
    PreviewBox = 5,
    Erase = 6
}

public class Paintbrush : MonoBehaviour {

    public const float DRAW_FRAMERATE = 30.5f;
    public const float DRAW_DELTA = 1f / DRAW_FRAMERATE;

    public static Action<BrushAction> OnDraw;

    public enum Tool {
        Freehand = 0,
        Line = 1,
        Box = 2,
        Oval = 3,
        FloodFill = 4
    }

    [SerializeField]
    private DrawingBoard _drawingBoard;

    [SerializeField]
    [FormerlySerializedAs("board")]
    private RectTransform _board;

    [SerializeField]
    [FormerlySerializedAs("maxBrushSize")]
    private int _maxBrushSize = 5;

    [SerializeField]
    [FormerlySerializedAs("scrollSensitivity")]
    private float _scrollSensitivity = 1;

    [SerializeField]
    [FormerlySerializedAs("eraseSize")]
    private int _eraseSize = 10;

    [Header("Paintbrush State")]
    [SerializeField]
    [FormerlySerializedAs("tool")]
    private Tool _tool = Tool.Freehand;

    [SerializeField]
    [FormerlySerializedAs("size")]
    private float _size = 0;

    [SerializeField]
    [FormerlySerializedAs("color")]
    private Color _color;

    private float _lastDrawTime;

    public int IntSize {
        get {
            return Mathf.RoundToInt(_size);
        }
    }

    public Vector2Int CurrCursor {
        get {
            Rect rect = rectTransformToScreenSpace(_board);

            float dx = inverseLerpUnclamped(rect.x, rect.x + rect.width, Input.mousePosition.x);
            float dy = inverseLerpUnclamped(rect.y, rect.y + rect.height, Input.mousePosition.y);

            int cx = Mathf.RoundToInt(dx * _drawingBoard.ResolutionX);
            int cy = Mathf.RoundToInt(dy * _drawingBoard.ResolutionY);

            return new Vector2Int(cx, cy);
        }
    }

    private bool isInsideCanvas(Vector2Int cursor) {
        return cursor.x >= 0 && cursor.x < _board.sizeDelta.x &&
               cursor.y >= 0 && cursor.y < _board.sizeDelta.y;
    }

    private bool TryExecuteDraw(BrushAction action, bool forceDraw = false) {
        if (forceDraw || (Time.time - _lastDrawTime) > DRAW_DELTA) {
            OnDraw?.Invoke(action);
            _lastDrawTime = Time.time;
            return true;
        } else {
            return false;
        }
    }

    #region UPDATE LOGIC
    private void Start() {
        StartCoroutine(controlCoroutine());
    }

    private void LateUpdate() {
        if (Player.Local == null) {
            return;
        }

        if (isInsideCanvas(CurrCursor)) {
            _size = Mathf.Clamp(_size - Input.mouseScrollDelta.y * _scrollSensitivity, 0, _maxBrushSize);
        }

        if (OnDraw != null && GameCoordinator.instance.CanPlayerDraw(Player.Local)) {
            var action = new BrushAction() {
                drawerId = Player.Local.NetworkObjectId,
                type = BrushActionType.PreviewBox,
                position0 = CurrCursor,
                size = IntSize,
                color = _color,
                isPreview = true
            };

            if (Input.GetKey(KeyCode.Mouse1)) {
                action.size = _eraseSize;
                action.color = Color.white;
            }

            if (!Input.GetKey(KeyCode.Mouse0) && !TryExecuteDraw(action)) {
                GameCoordinator.instance.DrawingBoard.PredictBrushAction(action);
            }
        }

        if (!GameCoordinator.instance.CanPlayerDraw(Player.Local)) {
            GameCoordinator.instance.DrawingBoard.PredictBrushAction(new BrushAction() {
                drawerId = Player.Local.NetworkObjectId,
                type = BrushActionType.Box,
                position0 = new Vector2Int(-100, -100),
                position1 = new Vector2Int(-100, -100),
                size = 0,
                color = Color.black,
                isPreview = true
            });
        }
    }

    IEnumerator controlCoroutine() {
        while (true) {
            while (!NetworkManager.Singleton.IsClient) {
                yield return null;
            }

            yield return null;

            if (!isInsideCanvas(CurrCursor)) {
                continue;
            }

            if (Input.GetKeyDown(KeyCode.Mouse0)) {
                switch (_tool) {
                    case Tool.Freehand:
                        yield return StartCoroutine(freeformCoroutine(erase: false));
                        break;
                    case Tool.Line:
                    case Tool.Box:
                    case Tool.Oval:
                        yield return StartCoroutine(shapeCoroutine());
                        break;
                    case Tool.FloodFill:
                        if (OnDraw != null && CurrCursor.x > 0 && CurrCursor.y > 0 && CurrCursor.x < _drawingBoard.ResolutionX && CurrCursor.y < _drawingBoard.ResolutionY) {
                            OnDraw(new BrushAction() {
                                type = BrushActionType.FloodFill,
                                position0 = CurrCursor,
                                color = _color
                            });
                        }

                        break;
                    default:
                        Debug.LogError("Should be no other tools, but had a tool of " + _tool);
                        break;
                }
            } else if (Input.GetKeyDown(KeyCode.Mouse1)) {
                yield return StartCoroutine(freeformCoroutine(erase: true));
            }
        }
    }

    IEnumerator freeformCoroutine(bool erase) {
        Vector2Int prevCursor = CurrCursor;
        BrushAction action;

        do {
            action = new BrushAction() {
                type = erase ? BrushActionType.Erase : BrushActionType.Line,
                position0 = prevCursor,
                position1 = CurrCursor,
                color = _color,
                size = erase ? _eraseSize : IntSize,
                drawerId = Player.Local.NetworkObjectId
            };

            if (TryExecuteDraw(action)) {
                prevCursor = CurrCursor;
            } else {
                action.isPreview = true;
                GameCoordinator.instance.DrawingBoard.PredictBrushAction(action);
            }

            yield return null;
        } while (Input.GetKey(KeyCode.Mouse0) || Input.GetKey(KeyCode.Mouse1));

        action.isPreview = false;
        TryExecuteDraw(action, forceDraw: true);
    }

    IEnumerator shapeCoroutine() {
        Vector2Int startCursor = CurrCursor;

        while (Input.GetKey(KeyCode.Mouse0)) {
            var action = new BrushAction() {
                type = (BrushActionType)_tool,
                position0 = startCursor,
                position1 = CurrCursor,
                color = _color,
                size = IntSize,
                isPreview = true
            };

            if (!TryExecuteDraw(action)) {
                action.isPreview = true;
                action.drawerId = Player.Local.NetworkObjectId;
                GameCoordinator.instance.DrawingBoard.PredictBrushAction(action);
            }

            yield return null;
        }

        TryExecuteDraw(new BrushAction() {
            type = (BrushActionType)_tool,
            position0 = startCursor,
            position1 = CurrCursor,
            color = _color,
            size = IntSize
        }, forceDraw: true);
    }

    public void SetTool(int newTool) {
        _tool = (Tool)newTool;
    }

    public void SetColor(Color color) {
        _color = color;
    }

    public void Clear() {
        OnDraw(new BrushAction() {
            type = BrushActionType.Clear
        });
    }
    #endregion

    private static float inverseLerpUnclamped(float min, float max, float value) {
        return (value - min) / (max - min);
    }

    private static Rect rectTransformToScreenSpace(RectTransform transform) {
        Vector2 size = transform.sizeDelta;

        Vector2 center = transform.position;

        return new Rect(center - size / 2, size);
    }

}
