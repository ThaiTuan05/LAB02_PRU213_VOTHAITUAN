using UnityEngine;
using static Lab2.SnowSynth;

namespace Lab2 {
    // Background music is synthesised at runtime instead of shipping audio files.
    //
    // Each route gets its own seamless loop: a chord pad, a bass line, an arpeggio or bell
    // motif, light percussion and an eighth-note delay whose tail folds back into the head of
    // the loop, so `AudioSource.loop` never produces a click at the seam. Clips are built once
    // and cached per route, so switching mountains back and forth costs nothing.
    public static class SnowMusic {
        static AudioClip[] cache = new AudioClip[3];

        public static AudioClip Theme(int route) {
            int index = Mathf.Clamp(route - 1, 0, 2);
            if (cache[index] == null) {
                float[] samples = index == 0 ? Ridge() : index == 1 ? Glacier() : Summit();
                cache[index] = Clip("Music " + (index + 1), samples);
            }
            return cache[index];
        }

        // ---- RIDGE RUN: bright, upbeat, C major ---------------------------------
        static float[] Ridge() {
            double beat = 60.0 / 96.0, bar = beat * 4.0;
            int[][] chords = {
                new[]{ 60, 64, 67 }, new[]{ 55, 59, 62 }, new[]{ 57, 60, 64 }, new[]{ 53, 57, 60 },
                new[]{ 60, 64, 67 }, new[]{ 55, 59, 62 }, new[]{ 53, 57, 60 }, new[]{ 55, 59, 62 }
            };
            int[] roots = { 48, 43, 45, 41, 48, 43, 41, 43 };
            var bus = Bus(chords.Length * bar);
            var rng = new Rng(20261016u);

            for (int b = 0; b < chords.Length; b++) {
                double at = b * bar;
                Chord(bus, chords[b], bar * 1.35, SnowVoice.Pad, .40, .55, .45, .004, 0.55, at);
                Mix(bus, Render(Freq(roots[b]), beat * 1.5, SnowVoice.Bass, .008, .12, 3.0, 0), .46, at);
                Mix(bus, Render(Freq(roots[b] + 7), beat * .9, SnowVoice.Bass, .008, .12, 3.6, 0), .30, at + beat * 2);
                Mix(bus, Render(Freq(roots[b] + 12), beat * .5, SnowVoice.Sine, .008, .10, 4.2, 0), .22, at + beat * 3.5);
                for (int s = 0; s < 8; s++) {
                    int note = chords[b][(s * 3) % chords[b].Length] + 12 + (s >= 4 ? 12 : 0);
                    Mix(bus, Render(Freq(note), beat * .5, SnowVoice.Pluck, .006, .08, 4.6, 0), s % 2 == 0 ? .15 : .095, at + s * beat * .5);
                }
                for (int s = 0; s < 8; s++) Hat(bus, rng, at + s * beat * .5, .08, s % 2 == 0 ? .028 : .015, 46);
                Kick(bus, at, .30, 16);
                Kick(bus, at + beat * 2, .26, 16);
                if (b >= 4) {
                    int[] motif = { 0, 2, 1, 2, 0, 1, 2, 1 };
                    for (int s = 0; s < 8; s++)
                        Mix(bus, Render(Freq(chords[b][motif[s]] + 24), beat * .65, SnowVoice.WarmPad, .006, .09, 3.8, 0),
                            (s == 0 || s == 4) ? .19 : .12, at + s * beat * .5);
                }
            }
            Space(bus, beat * .75, .22, .30);
            return SoftClip(bus, 0.82f);
        }

        // ---- GLACIER RIDGE: cool, airy, A minor --------------------------------
        static float[] Glacier() {
            double beat = 60.0 / 78.0, bar = beat * 4.0;
            int[][] chords = {
                new[]{ 57, 60, 64 }, new[]{ 53, 57, 60 }, new[]{ 52, 55, 60 }, new[]{ 55, 59, 62 },
                new[]{ 57, 60, 64 }, new[]{ 53, 57, 60 }, new[]{ 50, 53, 57 }, new[]{ 52, 56, 59 }
            };
            int[] roots = { 45, 41, 40, 43, 45, 41, 38, 40 };
            var bus = Bus(chords.Length * bar);
            var rng = new Rng(777123u);

            for (int b = 0; b < chords.Length; b++) {
                double at = b * bar;
                Chord(bus, chords[b], bar * 1.4, SnowVoice.WarmPad, .55, .75, .38, .005, 0.62, at);
                Mix(bus, Render(Freq(roots[b]), beat * 2.2, SnowVoice.Sine, .02, .30, 1.8, 0), .40, at);
                Mix(bus, Render(Freq(roots[b] + 12), beat * 1.1, SnowVoice.WarmPad, .02, .25, 2.4, 0), .22, at + beat * 2.5);
                int[] shape = { 0, 1, 2, 1 };
                for (int s = 0; s < 4; s++)
                    Mix(bus, Render(Freq(chords[b][shape[s]] + 24), beat * 1.2, SnowVoice.Bell, .008, .35, 2.6, 0),
                        s == 0 ? .17 : .105, at + beat * (1.5 + s * .5));
                Mix(bus, Render(Freq(chords[b][2] + 36), beat * 1.5, SnowVoice.Chime, .008, .40, 2.4, 0), .07, at + beat * 3.25);
                if (b % 2 == 1) Hat(bus, rng, at, .13, .018, 30);
                if (b == 3 || b == 7) Kick(bus, at, .22, 12);
            }
            Space(bus, beat * 1.5, .32, .40);
            return SoftClip(bus, 0.80f);
        }

        // ---- SUMMIT DESCENT: night drone, E minor ------------------------------
        static float[] Summit() {
            double beat = 60.0 / 68.0, bar = beat * 4.0;
            int[][] chords = {
                new[]{ 52, 55, 59 }, new[]{ 48, 52, 55 }, new[]{ 50, 55, 59 }, new[]{ 50, 54, 57 },
                new[]{ 52, 55, 59 }, new[]{ 45, 48, 52 }, new[]{ 50, 54, 57 }, new[]{ 47, 50, 54 }
            };
            int[] roots = { 40, 36, 43, 38, 40, 33, 38, 35 };
            var bus = Bus(chords.Length * bar);
            var rng = new Rng(31415926u);

            for (int b = 0; b < chords.Length; b++) {
                double at = b * bar;
                Chord(bus, chords[b], bar * 1.45, SnowVoice.Bass, .70, .90, .30, .006, 0.60, at);
                Mix(bus, Render(Freq(roots[b] - 12), beat * 1.9, SnowVoice.Sine, .05, .40, 1.3, 0), .38, at);
                Mix(bus, Render(Freq(roots[b]), beat * 1.3, SnowVoice.Bass, .02, .30, 2.0, 0), .24, at + beat * 2);
                int[] melody = { 0, 2, 1, 2, 0, 1, 2, 1 };
                for (int s = 0; s < 4; s++) {
                    int note = chords[b][melody[(b * 2 + s) % melody.Length]] + 24 + (s == 3 ? 12 : 0);
                    Mix(bus, Render(Freq(note), beat * 1.7, SnowVoice.Bell, .010, .45, 1.9, 0), s % 2 == 0 ? .16 : .095, at + s * beat);
                }
                if (b % 2 == 0) Mix(bus, Render(Freq(chords[b][2] + 24), bar * 1.1, SnowVoice.Chime, .6, .9, .45, 0), .05, at + beat * 2);
                Kick(bus, at, .30, 9);
                if (b % 4 == 3) Kick(bus, at + beat * 2.5, .17, 12);
                Hat(bus, rng, at + beat * 2, .11, .015, 26);
            }
            Space(bus, beat * 2.0, .36, .44);
            return SoftClip(bus, 0.78f);
        }
    }
}