using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameDebug : MonoBehaviour {

  public DrawingBoard board;
  public Image idImage;

  private Texture2D _idTex;

  private void Awake() {
    _idTex = new Texture2D(board.resolutionX, board.resolutionY, TextureFormat.ARGB32, mipChain: false);

    var sprite = Sprite.Create(_idTex, new Rect(0, 0, board.resolutionX, board.resolutionY), Vector2.zero);
    idImage.sprite = sprite;
  }

  private void Update() {
    if (Input.GetKeyDown(KeyCode.F9)) {
      if (idImage.gameObject.activeSelf) {
        idImage.gameObject.SetActive(false);
      } else {
        idImage.gameObject.SetActive(true);
      }
    }

    if (idImage.gameObject.activeSelf) {
      board.canvas.TryUpdateIdTexture(_idTex);
    }
  }







}
