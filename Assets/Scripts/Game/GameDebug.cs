using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class GameDebug : MonoBehaviour {

  [SerializeField]
  [FormerlySerializedAs("board")]
  private DrawingBoard _board;

  [SerializeField]
  [FormerlySerializedAs("idImage")]
  private Image _idImage;

  private Texture2D _idTex;

  private void Awake() {
    _idTex = new Texture2D(_board.ResolutionX, _board.ResolutionY, TextureFormat.ARGB32, mipChain: false);

    var sprite = Sprite.Create(_idTex, new Rect(0, 0, _board.ResolutionX, _board.ResolutionY), Vector2.zero);
    _idImage.sprite = sprite;
  }

  private void Update() {
    if (Input.GetKeyDown(KeyCode.F9)) {
      if (_idImage.gameObject.activeSelf) {
        _idImage.gameObject.SetActive(false);
      } else {
        _idImage.gameObject.SetActive(true);
      }
    }

    if (_idImage.gameObject.activeSelf) {
      _board.Canvas.TryUpdateIdTexture(_idTex);
    }
  }
}
