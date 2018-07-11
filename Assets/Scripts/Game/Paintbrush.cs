using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public struct BrushAction {
  public NetworkInstanceId drawerId;
  public BrushActionType type;
  public int x0, y0;
  public int x1, y1;
  public Color32 color;
  public int size;
  public float time;

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
}

public enum BrushActionType {
  Clear = 0,
  Line = 1,
  Box = 2,
  Oval = 3,
  FloodFill = 4
}

public class Paintbrush : MonoBehaviour {

  public static Action<BrushAction> OnPreview;
  public static Action OnClearPreview;
  public static Action<BrushAction> OnDraw;

  public enum Tool {
    Freehand = 0,
    Line = 1,
    Box = 2,
    Oval = 3,
    FloodFill = 4
  }

  public RectTransform board;
  public int maxBrushSize = 5;
  public float scrollSensitivity = 1;

  [Header("Paintbrush State")]
  public Tool tool = Tool.Freehand;
  public float size = 0;
  public Color color;

  private int intSize {
    get {
      return Mathf.RoundToInt(size);
    }
  }

  private Vector2Int currCursor {
    get {
      Rect rect = rectTransformToScreenSpace(board);

      float dx = inverseLerpUnclamped(rect.x, rect.x + rect.width, Input.mousePosition.x);
      float dy = inverseLerpUnclamped(rect.y, rect.y + rect.height, Input.mousePosition.y);

      int cx = Mathf.RoundToInt(dx * board.rect.width);
      int cy = Mathf.RoundToInt(dy * board.rect.height);

      return new Vector2Int(cx, cy);
    }
  }

  private bool currHeld {
    get {
      return Input.GetKey(KeyCode.Mouse0);
    }
  }

  private bool currPressed {
    get {
      return Input.GetKeyDown(KeyCode.Mouse0);
    }
  }

  private bool isInsideCanvas(Vector2Int cursor) {
    return cursor.x >= 0 && cursor.x < board.sizeDelta.x &&
           cursor.y >= 0 && cursor.y < board.sizeDelta.y;
  }

  #region UPDATE LOGIC
  private void Start() {
    SetTool((int)tool);
  }

  private void Update() {
    if (OnClearPreview != null) {
      OnClearPreview();
    }

    if (isInsideCanvas(currCursor)) {
      size = Mathf.Clamp(size - Input.mouseScrollDelta.y * scrollSensitivity, 0, maxBrushSize);

      if (OnPreview != null && !currHeld && GameCoordinator.instance.CanPlayerDraw(Player.local)) {
        OnPreview(new BrushAction() {
          type = BrushActionType.Line,
          position0 = currCursor,
          position1 = currCursor,
          size = intSize,
          color = color
        });

        OnPreview(new BrushAction() {
          type = BrushActionType.Box,
          position0 = currCursor - new Vector2Int(intSize, intSize),
          position1 = currCursor + new Vector2Int(intSize, intSize),
          size = 0,
          color = new Color(0, 0, 0, 1)
        });
      }
    }
  }

  IEnumerator freeformCoroutine() {
    Vector2Int prevCursor = Vector2Int.zero;
    bool prevHeld = false;

    while (true) {
      if (currHeld && prevHeld &&
          (isInsideCanvas(prevCursor) || isInsideCanvas(currCursor))) {

        if (OnDraw != null) {
          OnDraw(new BrushAction() {
            type = BrushActionType.Line,
            position0 = prevCursor,
            position1 = currCursor,
            color = color,
            size = intSize
          });
        }
      }

      prevHeld = currHeld;
      prevCursor = currCursor;
      yield return null;
    }
  }

  IEnumerator shapeCoroutine() {
    while (true) {
      //Wait until a click happens inside canvas
      yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.Mouse0) && isInsideCanvas(currCursor));
      Vector2Int startCursor = currCursor;

      //Draw a preview of the action while the cursor is held
      while (currHeld) {
        if (OnPreview != null) {
          OnPreview(new BrushAction() {
            type = (BrushActionType)tool,
            position0 = startCursor,
            position1 = currCursor,
            color = color,
            size = intSize
          });
        }

        yield return null;
      }

      if (OnClearPreview != null) {
        OnClearPreview();
      }

      if (OnDraw != null) {
        OnDraw(new BrushAction() {
          type = (BrushActionType)tool,
          position0 = startCursor,
          position1 = currCursor,
          color = color,
          size = intSize
        });
      }
    }
  }

  IEnumerator floodFillCoroutine() {
    while (true) {
      yield return new WaitUntil(() => currPressed && isInsideCanvas(currCursor));

      if (OnDraw != null) {
        OnDraw(new BrushAction() {
          type = BrushActionType.FloodFill,
          position0 = currCursor,
          color = color
        });
      }

      yield return new WaitWhile(() => currPressed);
    }
  }

  public void SetTool(int newTool) {
    tool = (Tool)newTool;
    StopAllCoroutines();

    switch (tool) {
      case Tool.Freehand:
        StartCoroutine(freeformCoroutine());
        break;
      case Tool.Line:
      case Tool.Box:
      case Tool.Oval:
        StartCoroutine(shapeCoroutine());
        break;
      case Tool.FloodFill:
        StartCoroutine(floodFillCoroutine());
        break;
      default:
        Debug.LogError("Tried to switch to unexpected tool " + tool);
        break;
    }
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
