using UnityEngine;
using static Lab2.SnowSynth;

namespace Lab2 {
    // Pickup sound effects, synthesised at runtime with the same primitives as the music.
    //
    // Snowflakes climb a C-major pentatonic ladder as the combo multiplier grows, so chaining
    // pickups is audible rather than just a bigger number on the HUD. Boost is a rising sweep,
    // Shield is a warm fifth that swells like a bubble forming, and every completed flip in the
    // air fires a Combo sting that climbs with the multiplier. Every clip is short and cached
    // up front, so collecting or chaining something never costs a frame.
    public static class SnowSfx {
        public enum Kind { Flake, Boost, Shield, Combo }
        public const int FlakeLevels = 8;

        static readonly AudioClip[] flakes = new AudioClip[FlakeLevels];
        static readonly AudioClip[] combos = new AudioClip[FlakeLevels];
        static AudioClip boost, shield;

        // C major pentatonic across two octaves, low to high.
        static readonly int[] flakeNotes = { 76, 79, 81, 84, 86, 88, 91, 93 };

        public static void Preload() {
            for (int level = 0; level < FlakeLevels; level++) { Flake(level + 1); Combo(level + 1); }
            Boost();
            Shield();
        }

        public static AudioClip Clip(Kind kind, int comboLevel) {
            switch (kind) {
                case Kind.Boost: return Boost();
                case Kind.Shield: return Shield();
                case Kind.Combo: return Combo(comboLevel);
                default: return Flake(comboLevel);
            }
        }

        public static AudioClip Flake(int comboLevel) {
            int level = Mathf.Clamp(comboLevel, 1, FlakeLevels) - 1;
            if (flakes[level] == null) {
                int note = flakeNotes[level];
                double dur = 0.34;
                var buffer = Bus(dur);
                // Main chime plus a quieter sparkle a fifth above, a few milliseconds later.
                Mix(buffer, Render(Freq(note), dur, SnowVoice.Chime, .002, .10, 9.0, 0), 1.0);
                Mix(buffer, Render(Freq(note + 7), dur * .8, SnowVoice.Bell, .002, .08, 13.0, 0), .38, .028);
                Mix(buffer, Render(Freq(note + 12), dur * .55, SnowVoice.Sine, .002, .06, 20.0, 0), .18, .055);
                flakes[level] = SnowSynth.Clip("Flake " + (level + 1), Normalise(buffer, 0.72f));
            }
            return flakes[level];
        }

        // Reward sting for a completed in-air rotation: a bright bell that climbs with the
        // multiplier, backed by a short low thump so it lands with some weight.
        public static AudioClip Combo(int comboLevel) {
            int level = Mathf.Clamp(comboLevel, 1, FlakeLevels) - 1;
            if (combos[level] == null) {
                int root = 67 + level;
                double dur = 0.45;
                var buffer = Bus(dur);
                Mix(buffer, Render(Freq(root + 12), dur, SnowVoice.Chime, .002, .12, 6.5, 0), 1.0);
                Mix(buffer, Render(Freq(root + 19), dur * .8, SnowVoice.Bell, .002, .10, 9.0, 0), .45, .035);
                Mix(buffer, Render(Freq(root + 24), dur * .6, SnowVoice.Sine, .002, .08, 14.0, 0), .25, .07);
                Mix(buffer, Render(Freq(root - 12), .28, SnowVoice.Sine, .004, .12, 12.0, 0), .45);
                combos[level] = SnowSynth.Clip("Combo " + (level + 1), Normalise(buffer, 0.78f));
            }
            return combos[level];
        }

        // Rising saw sweep with a noise whoosh riding on top of it.
        public static AudioClip Boost() {
            if (boost == null) {
                double dur = 0.55, from = 180.0, to = 1150.0;
                var buffer = Bus(dur);
                var rng = new Rng(90210u);
                double phase = 0.0, noise = 0.0, ratio = to / from;
                for (int i = 0; i < buffer.Length; i++) {
                    double t = i / (double)SampleRate;
                    double f = from * System.Math.Pow(ratio, t / dur);
                    double amp = Env(t, dur, .03, .20, 1.1);
                    noise += (rng.Next() - noise) * 0.35;
                    buffer[i] = (Tone(phase, SnowVoice.Pluck) * .55 + noise * .30) * amp;
                    phase += 2.0 * System.Math.PI * f / SampleRate;
                }
                // Low thump so the pickup lands with some weight.
                Mix(buffer, Render(95.0, 0.3, SnowVoice.Sine, .004, .12, 11.0, 0), .45);
                boost = SnowSynth.Clip("Boost", SoftClip(buffer, 0.80f));
            }
            return boost;
        }

        // Warm rising fifth with a shimmer on top, like a bubble filling up.
        public static AudioClip Shield() {
            if (shield == null) {
                double dur = 0.7;
                var buffer = Bus(dur);
                double root = Freq(64), fifth = Freq(71);
                for (int i = 0; i < buffer.Length; i++) {
                    double t = i / (double)SampleRate;
                    double swell = Env(t, dur, .06, .26, 1.6);
                    double drift = 1.0 + .05 * t;
                    double vibrato = 1.0 + .006 * Sin(2.0 * System.Math.PI * 5.5 * t);
                    buffer[i] = (Sin(2.0 * System.Math.PI * root * drift * vibrato * t) * .55
                               + Sin(2.0 * System.Math.PI * fifth * drift * vibrato * t) * .30
                               + Triangle(2.0 * System.Math.PI * root * 3 * drift * t) * .10) * swell;
                }
                Mix(buffer, Render(Freq(88), 0.22, SnowVoice.Sine, .004, .10, 12.0, 0), .40);
                shield = SnowSynth.Clip("Shield", Normalise(buffer, 0.74f));
            }
            return shield;
        }
    }
}