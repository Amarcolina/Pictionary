using UnityEngine;
using UnityEngine.UI;

public class PreviewBoard : MonoBehaviour {

  public int resolutionX, resolutionY;
  public Image image;

  private DrawableCanvas _canvas;

  private void Awake() {
    _canvas = new DrawableCanvas(resolutionX, resolutionY);

    var sprite = Sprite.Create(_canvas.texture, new Rect(0, 0, resolutionX, resolutionY), Vector2.zero);
    image.sprite = sprite;

    ClearPreviewBoard();
  }

  private void OnDestroy() {
    _canvas.Dispose();
  }

  public void HandlePreviewAction(BrushAction action) {
    _canvas.ApplyBrushAction(action);
  }

  public void ClearPreviewBoard() {
    _canvas.Clear(new Color(0, 0, 0, 0));
  }
}
