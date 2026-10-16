using UnityEngine;

namespace Lab2 {
    // Fixed timbre presets. Sine and Pad keep the pads soft, Bass and Pluck are the brighter
    // mid-range sounds, Bell and Chime cover the metallic pickups and melody bells.
    public enum SnowVoice { Sine = 1, Pad = 2, Bass = 3, WarmPad = 4, Pluck = 5, Bell = 6, Chime = 7 }

    // Shared DSP primitives for the runtime-generated music and sound effects.
    //
    // Everything is synthesised in doubles and converted to float samples at the end, so both
    // the looping themes and the short one-shots share the exact same oscillators and envelopes
    // and the project carries no imported audio for them.
    public static class SnowSynth {
        public const int SampleRate = 44100;

        // Math.Sin dominates the cost of these voices, so every oscillator reads a precomputed
        // table with linear interpolation. At 44.1 kHz that is inaudible and roughly an order of
        // magnitude faster, which keeps synthesis off the player's frame budget.
        const int TableSize = 8192;
        static readonly float[] Table = BuildTable();

        static float[] BuildTable() {
            var table = new float[TableSize];
            for (int i = 0; i < TableSize; i++) table[i] = (float)System.Math.Sin(2.0 * System.Math.PI * i / TableSize);
            return table;
        }

        public static double Sin(double phase) {
            double cycles = phase * (TableSize / (2.0 * System.Math.PI));
            int index = (int)cycles;
            double frac = cycles - index;
            index &= TableSize - 1;
            float a = Table[index], b = Table[(index + 1) & (TableSize - 1)];
            return a + (b - a) * frac;
        }

        public static double Triangle(double phase) {
            double f = phase * (1.0 / (2.0 * System.Math.PI));
            return 4.0 * System.Math.Abs(f - System.Math.Floor(f) - 0.5) - 1.0;
        }

        // Harmonic stacks instead of a raw ramp, so nothing aliases audibly.
        public static double Saw(double phase, int harmonics) {
            double v = 0.0, amp = 1.0, norm = 0.0;
            for (int h = 1; h <= harmonics; h++) { v += Sin(phase * h) * amp; norm += amp; amp /= h * 1.4; }
            return v / norm;
        }

        // Struck-string timbre: odd and even harmonics decay at different rates.
        public static double Bell(double phase, int harmonics) {
            double v = 0.0, norm = 0.0;
            for (int h = 1; h <= harmonics; h++) {
                double amp = 1.0 / System.Math.Pow(h, 1.4);
                if ((h & 1) == 0) amp *= -0.6;
                v += Sin(phase * h + (h > 1 ? 0.7 : 0.0)) * amp;
                norm += System.Math.Abs(amp);
            }
            return v / norm;
        }

        public static double Tone(double phase, SnowVoice voice) {
            switch (voice) {
                case SnowVoice.Pad: return Sin(phase) * .7 + Triangle(phase) * .3;
                case SnowVoice.Bass: return Sin(phase) * .8 + Saw(phase, 6) * .2;
                case SnowVoice.WarmPad: return Sin(phase) * .8 + Triangle(phase) * .2;
                case SnowVoice.Pluck: return Triangle(phase) * .6 + Saw(phase, 8) * .4;
                case SnowVoice.Bell: return Bell(phase, 7);
                case SnowVoice.Chime: return Bell(phase, 5);
                default: return Sin(phase);
            }
        }

        public static double Freq(int midi) { return 440.0 * System.Math.Pow(2.0, (midi - 69) / 12.0); }

        // attack -> decay -> release; always starts and ends at zero, which is what keeps the
        // music loops seamless and stops one-shots from clicking.
        public static double Env(double t, double dur, double attack, double release, double decay) {
            double a = attack > 0 ? System.Math.Min(1.0, t / attack) : 1.0;
            double r = release > 0 ? System.Math.Min(1.0, System.Math.Max(0.0, (dur - t) / release)) : 1.0;
            return a * r * System.Math.Exp(-t * decay);
        }

        public static int Length(double seconds) { return (int)System.Math.Round(seconds * SampleRate); }

        public static double[] Render(double freq, double dur, SnowVoice voice, double attack, double release, double decay, double detune) {
            var buffer = new double[Length(dur)];
            double phase = 0.0;
            for (int i = 0; i < buffer.Length; i++) {
                double t = i / (double)SampleRate;
                double v = Tone(phase, voice);
                if (detune > 0.0) v += Tone(phase * (1.0 + detune), voice);
                buffer[i] = v * Env(t, dur, attack, release, decay);
                phase += 2.0 * System.Math.PI * freq / SampleRate;
            }
            return buffer;
        }

        public static void Mix(double[] dst, double[] src, double gain, double atSeconds) {
            int start = (int)System.Math.Round(atSeconds * SampleRate);
            int from = start < 0 ? -start : 0;
            for (int i = from; i < src.Length; i++) {
                int j = start + i;
                if (j >= dst.Length) break;
                dst[j] += src[i] * gain;
            }
        }

        public static void Mix(double[] dst, double[] src, double gain) { Mix(dst, src, gain, 0.0); }

        public static void Chord(double[] dst, int[] notes, double dur, SnowVoice voice,
                                 double attack, double release, double decay, double detune, double gain, double atSeconds) {
            foreach (int note in notes) Mix(dst, Render(Freq(note), dur, voice, attack, release, decay, detune), gain / notes.Length, atSeconds);
        }

        public static double[] Bus(double seconds) { return new double[Length(seconds)]; }

        // ---- percussion ------------------------------------------------------

        public struct Rng {
            uint s;
            public Rng(uint seed) { s = seed | 1u; }
            public double Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s / 4294967295.0) * 2.0 - 1.0; }
        }

        public static void Hat(double[] dst, Rng rng, double at, double dur, double gain, double decay) {
            int start = (int)System.Math.Round(at * SampleRate);
            int n = (int)System.Math.Round(dur * SampleRate);
            double lp = 0.0;
            for (int i = 0; i < n; i++) {
                int j = start + i;
                if (j < 0) continue;
                if (j >= dst.Length) break;
                lp += (rng.Next() - lp) * 0.6;
                dst[j] += lp * gain * System.Math.Exp(-i / (double)SampleRate * decay);
            }
        }

        public static void Kick(double[] dst, double at, double gain, double drop) {
            int start = (int)System.Math.Round(at * SampleRate);
            int n = (int)System.Math.Round(0.28 * SampleRate);
            for (int i = 0; i < n; i++) {
                int j = start + i;
                if (j < 0) continue;
                if (j >= dst.Length) break;
                double t = i / (double)SampleRate;
                double f = 130.0 * System.Math.Exp(-t * drop) + 42.0;
                dst[j] += Sin(2.0 * System.Math.PI * f * t) * System.Math.Exp(-t * 12.0) * System.Math.Min(1.0, t / 0.004) * gain;
            }
        }

        // ---- output ----------------------------------------------------------

        // Delay line wraps at the buffer length, so the echo tail folds back into the head
        // instead of being cut off where a loop restarts.
        public static void Space(double[] buf, double delaySeconds, double wet, double feedback) {
            int n = buf.Length;
            int d = System.Math.Max(1, (int)System.Math.Round(delaySeconds * SampleRate));
            var outBuf = new double[n];
            for (int i = 0; i < n; i++) {
                int j = i - d;
                if (j < 0) j += n;
                outBuf[i] = buf[i] + buf[j] * wet + outBuf[j] * feedback;
            }
            System.Array.Copy(outBuf, buf, n);
        }

        public static float[] SoftClip(double[] buf, float peak) {
            var result = new float[buf.Length];
            float max = 0.0001f;
            for (int i = 0; i < buf.Length; i++) {
                double v = System.Math.Tanh(buf[i] * 1.2) * 0.85;
                result[i] = (float)v;
                float a = System.Math.Abs(result[i]);
                if (a > max) max = a;
            }
            float gain = peak / max;
            for (int i = 0; i < result.Length; i++) result[i] *= gain;
            return result;
        }

        public static float[] Normalise(double[] buf, float peak) {
            var result = new float[buf.Length];
            float max = 0.0001f;
            for (int i = 0; i < buf.Length; i++) { result[i] = (float)buf[i]; float a = System.Math.Abs(result[i]); if (a > max) max = a; }
            float gain = peak / max;
            for (int i = 0; i < result.Length; i++) result[i] *= gain;
            return result;
        }

        public static AudioClip Clip(string name, float[] samples) {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}