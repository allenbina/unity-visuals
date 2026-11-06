using System;
using UnityEngine;

namespace DjVisuals
{
    /// <summary>
    /// One internal beat clock. BPM sets the rate, Sync sets the phase.
    /// Phase is kept as an absolute start time so it survives frame hiccups and BPM edits.
    /// </summary>
    public class BeatClock : MonoBehaviour
    {
        public const int BeatsPerBar = 4;

        public double Bpm { get; private set; } = 120.0;
        public bool Running { get; private set; }
        public int BeatIndex { get; private set; } = -1;   // beats since downbeat (0 = downbeat)
        public float BeatPhase { get; private set; }        // 0..1 within the current beat
        /// <summary>Continuous position in beats (beat 0 = the synced downbeat). Free-runs at the BPM until Sync is pressed.</summary>
        public float BeatPos { get; private set; }
        public int BeatInBar => Running && BeatIndex >= 0 ? BeatIndex % BeatsPerBar : -1;

        public event Action<int> OnBeat;   // arg: beat index since sync

        double startTime;

        static double Now => Time.realtimeSinceStartupAsDouble;

        public void SetBpm(double bpm)
        {
            bpm = Math.Max(20.0, Math.Min(300.0, bpm));
            if (Running)
            {
                // Keep the current beat position when the rate changes.
                double beatPos = (Now - startTime) * Bpm / 60.0;
                startTime = Now - beatPos * 60.0 / bpm;
            }
            Bpm = bpm;
        }

        /// <summary>Call on the first beat of a bar: starts the clock or re-aligns its phase.</summary>
        public void Sync()
        {
            startTime = Now;
            Running = true;
            BeatIndex = -1;   // next Update fires beat 0 (the downbeat) immediately
        }

        void Update()
        {
            if (!Running) { BeatPhase = 0; BeatPos += Time.deltaTime * (float)Bpm / 60f; return; }
            double beatPos = (Now - startTime) * Bpm / 60.0;
            BeatPos = (float)beatPos;
            int idx = (int)Math.Floor(beatPos);
            BeatPhase = (float)(beatPos - idx);
            // Fire every beat crossed (normally one) so frame drops don't lose beats.
            while (BeatIndex < idx)
            {
                BeatIndex++;
                OnBeat?.Invoke(BeatIndex);
            }
        }
    }
}
