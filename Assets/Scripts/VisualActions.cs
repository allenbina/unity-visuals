using System;
using UnityEngine;

namespace DjVisuals
{
    /// <summary>
    /// The single set of scene actions. Keyboard, on-screen buttons, and later MIDI all call these.
    /// Worlds subscribe to the events and never care where an action came from.
    /// </summary>
    public class VisualActions : MonoBehaviour
    {
        public BeatClock Clock;

        public float Build { get; private set; }
        public int Progression { get; private set; }

        public event Action<int> Beat;          // beat index (clock) or -1 (manual pulse)
        public event Action Accent;
        public event Action Drop;
        public event Action<int> Progressed;    // new level
        public event Action ResetRequested;
        public event Action<bool> WarpChanged;  // true = engaging

        public bool Warping { get; private set; }

        // Not Awake: Bootstrap assigns Clock after AddComponent, so Awake would see null.
        void Start() { Clock.OnBeat += i => Beat?.Invoke(i); }

        public void SetBpm(double bpm) => Clock.SetBpm(bpm);
        public void Sync() => Clock.Sync();
        public void ManualBeat() => Beat?.Invoke(-1);
        public void FireAccent() => Accent?.Invoke();
        public void SetBuild(float v) => Build = Mathf.Clamp01(v);
        public void NudgeBuild(float d) => SetBuild(Build + d);
        public void FireDrop() { Drop?.Invoke(); }
        public void Advance() { Progression++; Progressed?.Invoke(Progression); }
        public void ToggleWarp() { Warping = !Warping; WarpChanged?.Invoke(Warping); }
        public void DoReset()
        {
            Build = 0; Progression = 0;
            if (Warping) { Warping = false; WarpChanged?.Invoke(false); }
            ResetRequested?.Invoke();
        }
    }
}
