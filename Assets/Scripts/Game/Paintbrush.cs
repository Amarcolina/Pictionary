using System;
using System.Collections;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.Serialization;

public struct BrushAction : INetworkSerializable {
    public ulong drawerId;
    public BrushActionType type;
    public int x0, y0;
    public int x1, y1;
    public Color32 color;
    public int size;
    public float time;
    public bool isPreview;

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
        serializer.SerializeValue(ref drawerId);
        serializer.SerializeValue(ref type);
        serializer.SerializeValue(ref time);
        serializer.SerializeValue(ref isPreview);

        switch (type) {
            case BrushActionType.Clear:
                break;
            case BrushActionType.Line:
            case BrushActionType.Box:
            case BrushActionType.Oval:
            case BrushActionType.PreviewBox:
                serializer.SerializeValue(ref x0);
                serializer.SerializeValue(ref y0);
                serializer.SerializeValue(ref x1);
                serializer.SerializeValue(ref y1);

                serializer.SerializeValue(ref color);
                serializer.SerializeValue(ref size);
                break;
            case BrushActionType.FloodFill:
                serializer.SerializeValue(ref x0);
                serializer.SerializeValue(ref y0);

                serializer.SerializeValue(ref color);
                break;
        }
    }

    public void Serialize(FastBufferWriter writer) {
        writer.WriteValue(drawerId);
        writer.WriteValue((byte)type);
        writer.WriteValue(time);
        writer.WriteValue(isPreview);

        switch (type) {
            case BrushActionType.Clear:
                break;
            case BrushActionType.Line:
            case BrushActionType.Box:
            case BrushActionType.Oval:
            case BrushActionType.PreviewBox:
                writer.WriteValue((ushort)position0.x);
                writer.WriteValue((ushort)position0.y);
                writer.WriteValue((ushort)position1.x);
                writer.WriteValue((ushort)position1.y);
                writer.WriteValue(color);
                writer.WriteValue((byte)size);
                break;
            case BrushActionType.FloodFill:
                writer.WriteValue((ushort)position0.x);
                writer.WriteValue((ushort)position0.y);
                writer.WriteValue(color);
                break;
        }
    }

    public void Deserialize(FastBufferReader reader) {
        reader.ReadValue(out drawerId);

        reader.ReadValue(out byte typeData);
        type = (BrushActionType)typeData;

        reader.ReadValue(out time);
        reader.ReadValue(out isPreview);

        switch (type) {
            case BrushActionType.Clear:
                break;
            case BrushActionType.Line:
            case BrushActionType.Box:
            case BrushActionType.Oval:
            case BrushActionType.PreviewBox: {
                    reader.ReadValue(out ushort x0Data);
                    reader.ReadValue(out ushort y0Data);
                    reader.ReadValue(out ushort x1Data);
                    reader.ReadValue(out ushort y1Data);
                    reader.ReadValue(out byte sizeData);
                    reader.ReadValue(out color);


                    x0 = (short)x0Data;
                    y0 = (short)y0Data;
                    x1 = (short)x1Data;
                    y1 = (short)y1Data;
                    size = (byte)sizeData;
                    break;
                }
            case BrushActionType.FloodFill: {
                    reader.ReadValue(out ushort x0Data);
                    reader.ReadValue(out ushort y0Data);
                    reader.ReadValue(out color);

                    x0 = (short)x0Data;
                    y0 = (short)y0Data;
                    break;
                }
        }
    }


}

public enum BrushActionType {
    Clear = 0,
    Line = 1,
    Box = 2,
    Oval = 3,
    FloodFill = 4,
    PreviewBox = 5
}

public class Paintbrush : MonoBehaviour {

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

    #region UPDATE LOGIC
    private void Start() {
        StartCoroutine(controlCoroutine());
    }

    private void Update() {
        if (!NetworkManager.Singleton.IsClient) {
            return;
        }

        if (isInsideCanvas(CurrCursor)) {
            _size = Mathf.Clamp(_size - Input.mouseScrollDelta.y * _scrollSensitivity, 0, _maxBrushSize);
        }

        if (OnDraw != null && GameCoordinator.instance.CanPlayerDraw(Player.Local)) {
            if (!Input.GetKey(KeyCode.Mouse0)) {
                OnDraw(new BrushAction() {
                    type = BrushActionType.PreviewBox,
                    position0 = CurrCursor,
                    size = IntSize,
                    color = _color,
                    isPreview = true
                });
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
                        yield return StartCoroutine(freeformCoroutine());
                        break;
                    case Tool.Line:
                    case Tool.Box:
                    case Tool.Oval:
                        yield return StartCoroutine(shapeCoroutine());
                        break;
                    case Tool.FloodFill:
                        if (OnDraw != null) {
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
                Debug.Log("Asd?");
                yield return StartCoroutine(eraseCoroutine());
            }
        }
    }

    IEnumerator eraseCoroutine() {
        Vector2Int prevCursor = CurrCursor;

        while (Input.GetKey(KeyCode.Mouse1)) {
            if (OnDraw != null) {
                OnDraw(new BrushAction() {
                    type = BrushActionType.Line,
                    position0 = prevCursor,
                    position1 = CurrCursor,
                    color = new Color(1, 1, 1, 1),
                    size = _eraseSize
                });

                OnDraw(new BrushAction() {
                    type = BrushActionType.Box,
                    position0 = CurrCursor - new Vector2Int(_eraseSize, _eraseSize),
                    position1 = CurrCursor + new Vector2Int(_eraseSize, _eraseSize),
                    color = new Color(0, 0, 0, 1),
                    size = 0,
                    isPreview = true
                });
            }

            prevCursor = CurrCursor;
            yield return null;
        }
    }

    IEnumerator freeformCoroutine() {
        Vector2Int prevCursor = CurrCursor;

        while (Input.GetKey(KeyCode.Mouse0)) {
            if (OnDraw != null) {
                OnDraw(new BrushAction() {
                    type = BrushActionType.Line,
                    position0 = prevCursor,
                    position1 = CurrCursor,
                    color = _color,
                    size = IntSize
                });
            }

            prevCursor = CurrCursor;
            yield return null;
        }
    }

    IEnumerator shapeCoroutine() {
        Vector2Int startCursor = CurrCursor;

        while (Input.GetKey(KeyCode.Mouse0)) {
            if (OnDraw != null) {
                OnDraw(new BrushAction() {
                    type = (BrushActionType)_tool,
                    position0 = startCursor,
                    position1 = CurrCursor,
                    color = _color,
                    size = IntSize,
                    isPreview = true
                });
            }

            yield return null;
        }

        if (OnDraw != null) {
            OnDraw(new BrushAction() {
                type = (BrushActionType)_tool,
                position0 = startCursor,
                position1 = CurrCursor,
                color = _color,
                size = IntSize
            });
        }
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
