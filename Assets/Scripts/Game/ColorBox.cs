using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ColorBox : MonoBehaviour {

  public Paintbrush brush;

  private Image _image;

  private void Start() {
    _image = GetComponent<Image>();

    GetComponent<Button>().onClick.AddListener(onClick);
  }

  private void onClick() {
    brush.color = _image.color;
  }
}
