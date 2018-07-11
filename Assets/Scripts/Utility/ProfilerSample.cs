using System;
using UnityEngine.Profiling;

public struct ProfilerSample : IDisposable {

  public ProfilerSample(string tag) {
    Profiler.BeginSample(tag);
  }

  public void Dispose() {
    Profiler.EndSample();
  }
}
