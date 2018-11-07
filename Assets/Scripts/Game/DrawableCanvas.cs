using System;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using UnityObject = UnityEngine.Object;
using Unity.Collections.LowLevel.Unsafe;

/// <summary>
/// Represents a canvas that can be drawn to in different ways.
/// It supports drawing lines, boxes, and ellipses, as well as
/// clearing the canvas, or performing a flood fill operation.
/// </summary>
public class DrawableCanvas : IDisposable {
  public const bool FLOOD_FILL_PRECALCULATION_ENABLED_BY_DEFAULT =
#if UNITY_EDITOR
    false;
#else
    false;
#endif

  private Texture2D _tex;
  private int _width, _height;
  private NativeArray<int> _ids;

  private bool _isDirty = false;
  private JobHandle? _scanlineJob;
  private bool _enableFloodFillPrecalculation;

  /// <summary>
  /// Constructs a new DrawableCanvas with a given width and height.
  /// 
  /// Can also optionally specify whether or not this canvas will support
  /// flood fill operations.  Revoking flood fill support will prevent the 
  /// canvas from performing background tasks during normal drawing that
  /// would use up extra CPU cycles.
  /// </summary>
  public DrawableCanvas(int width, int height, bool enableFloodFillPrecalculation = FLOOD_FILL_PRECALCULATION_ENABLED_BY_DEFAULT) {
    if (width <= 0) {
      throw new ArgumentException("The width of a DrawableCanvas must be greater than zero.");
    }
    if (height <= 0) {
      throw new ArgumentException("The height of a DrawableCanvas must be greater than zero.");
    }

    _width = width;
    _height = height;

    _tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
    _tex.wrapMode = TextureWrapMode.Clamp;
    _tex.filterMode = FilterMode.Point;

    _ids = new NativeArray<int>(width * height, Allocator.Persistent);
    _enableFloodFillPrecalculation = enableFloodFillPrecalculation;

    Clear();
  }

  /// <summary>
  /// Returns the texture object that represents this 
  /// canvas.  It is always up to date with any draw commands
  /// that have been requested.
  /// </summary>
  public Texture2D Texture {
    get {
      return _tex;
    }
  }

  /// <summary>
  /// Returns the width of this canvas.
  /// </summary>
  public int Width {
    get {
      return _width;
    }
  }

  /// <summary>
  /// Returns the height of this canvas.
  /// </summary>
  public int Height {
    get {
      return _height;
    }
  }

  /// <summary>
  /// Returns whether or not the canvas is currently performing
  /// any background tasks that have yet to complete.
  /// </summary>
  public bool IsProcessing {
    get {
      return _scanlineJob.HasValue;
    }
  }

  /// <summary>
  /// Destroys the canvas texture and any other resources.
  /// </summary>
  public void Dispose() {
    _ids.Dispose();
    UnityObject.DestroyImmediate(_tex);

    //If the job is currently underway, make sure we 
    //complete it!
    if (_scanlineJob.HasValue) {
      _scanlineJob.Value.Complete();
    }

    _tex = null;
    _scanlineJob = null;
  }

  public void TryUpdateIdTexture(Texture2D tex) {
    if (_scanlineJob.HasValue) {
      return;
    }

    Color32[] pixels = new Color32[_ids.Length];
    for (int i = 0; i < pixels.Length; i++) {
      UnityEngine.Random.InitState(_ids[i]);
      pixels[i].r = (byte)UnityEngine.Random.Range(0, 256);
      pixels[i].g = (byte)UnityEngine.Random.Range(0, 256);
      pixels[i].b = (byte)UnityEngine.Random.Range(0, 256);
      pixels[i].a = 255;
    }

    tex.SetPixels32(pixels);
    tex.Apply();
  }

  /// <summary>
  /// Updates the internal state of the canvas.  It is required to call this
  /// method every frame so that the canvas may perform any background tasks
  /// that it needs to.
  /// </summary>
  public void Update() {
    if (_enableFloodFillPrecalculation) {
      if (_scanlineJob.HasValue && _scanlineJob.Value.IsCompleted) {
        _scanlineJob.Value.Complete();
        _scanlineJob = null;
      }

      if (_isDirty && !_scanlineJob.HasValue) {
        _scanlineJob = new ScanlineFillJob() {
          colors = getTempJobTextureArray(),
          ids = _ids,
          width = _width,
          height = _height
        }.Schedule();
        _isDirty = false;
      }
    }
  }

  /// <summary>
  /// Performs the given draw action on this canvas.s
  /// </summary>
  public void ApplyBrushAction(BrushAction action) {
    switch (action.type) {
      case BrushActionType.Clear:
        Clear();
        break;
      case BrushActionType.Line:
        DrawLine(action.position0, action.position1, action.color, action.size);
        break;
      case BrushActionType.Box:
        DrawBox(action.position0, action.position1, action.color, action.size);
        break;
      case BrushActionType.Oval:
        DrawEllipse(action.position0, action.position1, action.color, action.size);
        break;
      case BrushActionType.FloodFill:
        Fill(action.position0, action.color);
        break;
      case BrushActionType.PreviewBox:
        DrawLine(action.position0, action.position0, action.color, action.size);
        DrawBox(action.position0 - new Vector2Int(action.size, action.size),
                action.position0 + new Vector2Int(action.size, action.size),
                new Color32(0, 0, 0, 255),
                0);
        break;
      default:
        throw new ArgumentException("Unexpected brush type " + action.type);
    }
  }

  /// <summary>
  /// Clears the canvas to the color white.
  /// </summary>
  public void Clear() {
    Clear(Color.white);
  }

  /// <summary>
  /// Sets all pixels on the canvas to a specific color.
  /// </summary>
  public void Clear(Color32 color) {
    using (new ProfilerSample("Clear")) {
      var textureData = _tex.GetRawTextureData<Color32>();

      unsafe {
        var colorSrc = new NativeArray<Color32>(1, Allocator.Temp);
        colorSrc[0] = color;

        UnsafeUtility.MemCpyReplicate(textureData.GetUnsafePtr(), colorSrc.GetUnsafePtr(), 4, textureData.Length);
        colorSrc.Dispose();
      }

      _tex.Apply();
      _isDirty = true;
    }
  }

  /// <summary>
  /// Draws a line of a given size from one position to another.
  /// </summary>
  public void DrawLine(Vector2Int p0, Vector2Int p1, Color32 col, int size) {
    using (new ProfilerSample("Draw Line")) {
      NativeArray<Color32> texData = _tex.GetRawTextureData<Color32>();
      for (int dx = -size; dx <= size; dx++) {
        for (int dy = -size; dy <= size; dy++) {
          Vector2Int offset = new Vector2Int(dx, dy);
          drawLine(texData, p0 + offset, p1 + offset, col);
        }
      }
      _tex.Apply();
      _isDirty = true;
    }
  }

  /// <summary>
  /// Draws a box with a given size.  The two positions specified represent
  /// two opposing corners of the drawn box.
  /// </summary>
  public void DrawBox(Vector2Int p0, Vector2Int p1, Color32 col, int size) {
    using (new ProfilerSample("Draw Box")) {
      var texData = _tex.GetRawTextureData<Color32>();
      for (int dx = -size; dx <= size; dx++) {
        for (int dy = -size; dy <= size; dy++) {
          Vector2Int offset = new Vector2Int(dx, dy);
          drawBox(texData, p0 + offset, p1 + offset, col);
        }
      }
      _tex.Apply();
      _isDirty = true;
    }
  }

  /// <summary>
  /// Draws an ellipse with a given size.  The two positions specified represent
  /// two opposing corners of the rectangle that the ellipse is inside of.
  /// </summary>
  public void DrawEllipse(Vector2Int p0, Vector2Int p1, Color32 col, int size) {
    using (new ProfilerSample("Draw Ellipse")) {
      Vector2Int center = p1 + p0;
      center.x /= 2;
      center.y /= 2;

      Vector2Int extent = p1 - p0;
      extent.x = Mathf.Abs(extent.x / 2);
      extent.y = Mathf.Abs(extent.y / 2);

      var texData = _tex.GetRawTextureData<Color32>();
      for (int dx = -size; dx <= size; dx++) {
        for (int dy = -size; dy <= size; dy++) {
          Vector2Int offset = new Vector2Int(dx, dy);
          int l = dx * dx + dy * dy;
          if (l > size * size) {
            continue;
          }

          drawEllipse(texData, center + offset, extent, col);
        }
      }
      _tex.Apply();
      _isDirty = true;
    }
  }

  /// <summary>
  /// Performs a flood fill operation at the given position with the given color.
  /// All pixels connected to the target pixel have their color changed to the
  /// fill color.
  /// </summary>
  public void Fill(Vector2Int position, Color fillColor) {
    using (new ProfilerSample("Fill")) {
      if (_scanlineJob.HasValue) {
        _scanlineJob.Value.Complete();
      }

      if (_isDirty) {
        using (new ProfilerSample("Clean Texture")) {
          new ScanlineFillJob() {
            colors = getTempJobTextureArray(),
            ids = _ids,
            width = _width,
            height = _height
          }.Run();
          _isDirty = false;
        }
      }

      int targetId = _ids[position.y * _width + position.x];
      while (targetId != _ids[targetId]) {
        targetId = _ids[targetId];
      }

      var textureData = _tex.GetRawTextureData<Color32>();
      new FloodFillJob() {
        colors = textureData,
        ids = _ids,
        idToFill = targetId,
        fillColor = fillColor
      }.Schedule(textureData.Length, _width).Complete();

      _tex.Apply();
      _isDirty = true;
    }
  }

  private void drawLine(NativeArray<Color32> texData, Vector2Int p0, Vector2Int p1, Color32 col) {
    int dy = p1.y - p0.y;
    int dx = p1.x - p0.x;
    int stepx, stepy;

    if (dy < 0) { dy = -dy; stepy = -1; } else { stepy = 1; }
    if (dx < 0) { dx = -dx; stepx = -1; } else { stepx = 1; }
    dy <<= 1;
    dx <<= 1;

    int fraction = 0;

    setSafe(texData, p0.x, p0.y, col);
    if (dx > dy) {
      fraction = dy - (dx >> 1);
      while (Mathf.Abs(p0.x - p1.x) > 1) {
        if (fraction >= 0) {
          p0.y += stepy;
          fraction -= dx;
        }
        p0.x += stepx;
        fraction += dy;
        setSafe(texData, p0.x, p0.y, col);
      }
    } else {
      fraction = dx - (dy >> 1);
      while (Mathf.Abs(p0.y - p1.y) > 1) {
        if (fraction >= 0) {
          p0.x += stepx;
          fraction -= dy;
        }
        p0.y += stepy;
        fraction += dx;
        setSafe(texData, p0.x, p0.y, col);
      }
    }
  }

  private void drawBox(NativeArray<Color32> texData, Vector2Int p0, Vector2Int p1, Color32 col) {
    int fromX = p0.x < p1.x ? p0.x : p1.x;
    int toX = p0.x > p1.x ? p0.x : p1.x;
    int fromY = p0.y < p1.y ? p0.y : p1.y;
    int toY = p0.y > p1.y ? p0.y : p1.y;

    for (int x = fromX; x <= toX; x++) {
      setSafe(texData, x, fromY, col);
      setSafe(texData, x, toY, col);
    }

    for (int y = fromY; y <= toY; y++) {
      setSafe(texData, fromX, y, col);
      setSafe(texData, toX, y, col);
    }
  }

  private void drawEllipse(NativeArray<Color32> texData, Vector2Int center, Vector2Int extent, Color32 color) {
    if (extent.x == 0 || extent.y == 0) {
      drawLine(texData, center - extent, center + extent, color);
      return;
    }

    int a2 = extent.x * extent.x;
    int b2 = extent.y * extent.y;
    int fa2 = 4 * a2, fb2 = 4 * b2;
    int x, y, sigma;

    //first half
    for (x = 0, y = extent.y, sigma = 2 * b2 + a2 * (1 - 2 * extent.y); b2 * x <= a2 * y; x++) {
      int x0 = center.x + x;
      int x1 = center.x - x;
      int y0 = center.y + y;
      int y1 = center.y - y;

      setSafe(texData, x0, y0, color);
      setSafe(texData, x1, y0, color);
      setSafe(texData, x0, y1, color);
      setSafe(texData, x1, y1, color);

      if (sigma >= 0) {
        sigma += fa2 * (1 - y);
        y--;
      }
      sigma += b2 * ((4 * x) + 6);
    }

    //second half
    for (x = extent.x, y = 0, sigma = 2 * a2 + b2 * (1 - 2 * extent.x); a2 * y <= b2 * x; y++) {
      int x0 = center.x + x;
      int x1 = center.x - x;
      int y0 = center.y + y;
      int y1 = center.y - y;

      setSafe(texData, x0, y0, color);
      setSafe(texData, x1, y0, color);
      setSafe(texData, x0, y1, color);
      setSafe(texData, x1, y1, color);

      if (sigma >= 0) {
        sigma += fb2 * (1 - x);
        x--;
      }
      sigma += a2 * ((4 * y) + 6);
    }
  }

  private struct ScanlineFillJob : IJob {
    [ReadOnly, DeallocateOnJobCompletion]
    public NativeArray<Color32> colors;
    public NativeArray<int> ids;

    public int width, height;

    public void Execute() {
      Color32 currColor;
      int currId;

      //Set up first scanline
      currColor = colors[0];
      currId = 0;
      for (int x = 0; x < width; x++) {
        if (!equals32(colors[x], currColor)) {
          currColor = colors[x];
          currId = x;
        }

        ids[x] = currId;
      }

      //Perform the rest of the scanlines
      int index = width;
      for (int y = 1; y < height; y++) {
        currColor = colors[index];
        currId = index;
        ids[index] = currId;

        index++;
        for (int x = 1; x < width; x++) {
          if (!equals32(colors[index], currColor)) {
            currColor = colors[index];
            currId = index;
            ids[currId] = currId;
          }

          if (equals32(currColor, colors[index - width])) {
            int id = getId(x, y - 1);
            linkId(currId, id);
            currId = id;
          }

          ids[index] = currId;
          index++;
        }
      }
    }

    private int getId(int idSrc) {
      while (ids[idSrc] != idSrc) {
        idSrc = ids[idSrc];
      }
      return idSrc;
    }

    private void linkId(int toLink, int srcId) {
      while (true) {
        int nextId = ids[toLink];
        ids[toLink] = srcId;

        if (nextId == toLink) {
          return;
        }

        toLink = nextId;
      }
    }

    private int getId(int x, int y) {
      return getId(ids[y * width + x]);
    }

    private Color32 getColor(int x, int y) {
      return colors[y * width + x];
    }
  }

  private struct FloodFillJob : IJobParallelFor {
    [WriteOnly]
    public NativeArray<Color32> colors;
    [ReadOnly]
    public NativeArray<int> ids;
    public int idToFill;
    public Color32 fillColor;

    public void Execute(int i) {
      int id = ids[i];

      while (true) {
        if (id == idToFill) {
          colors[i] = fillColor;
          return;
        }

        int nextId = ids[id];
        if (nextId == id) {
          return;
        }

        id = nextId;
      }
    }
  }

  private NativeArray<Color32> getTempJobTextureArray() {
    var backingData = _tex.GetRawTextureData<Color32>();
    return new NativeArray<Color32>(backingData, Allocator.TempJob);
  }

  private void setSafe(NativeArray<Color32> texData, int x, int y, Color32 color) {
    if (x < 0 || x >= _width || y < 0 || y >= _height) {
      return;
    }

    texData[y * _width + x] = color;
  }

  private static bool equals32(Color32 a, Color32 b) {
    return a.r == b.r &&
           a.g == b.g &&
           a.b == b.b;
  }
}
