using UnityEngine;

//public class NetworkedTime {

//  private float _updateTimestamp;
//  private float _updateValue;

//  private float _rate;
//  private float _prevValue;

//  public float Rate {
//    get {
//      return _rate;
//    }
//    set {
//      _rate = value;
//    }
//  }

//  public float Value {
//    get {
//      float newValue = (Time.realtimeSinceStartup - _updateTimestamp) * _rate + _updateValue;

//      if (_rate < 0) {
//        newValue = Mathf.Min(newValue, _prevValue);
//      } else {
//        newValue = Mathf.Max(newValue, _prevValue);
//      }

//      _prevValue = newValue;

//      return newValue;
//    }
//  }

//  public void Update(float time, bool forceUpdate) {
//    if (forceUpdate) {
//      _prevValue = time;
//    }

//    _updateTimestamp = Time.realtimeSinceStartup;
//    _updateValue = time;
//  }

//  public static implicit operator float(NetworkedTime time) {
//    return time.Value;
//  }
//}
