using System;
using System.IO;
using System.Media;

namespace DNSParty.Services
{
    public class SoundService
    {
        private byte[] _connectWavBytes;
        private byte[] _disconnectWavBytes;

        public SoundService()
        {
            EnsureSoundFiles();
        }

        public void EnsureSoundFiles()
        {
            try
            {
                if (_connectWavBytes == null)
                {
                    // Ascending two-tone chime: D5 (587.33 Hz) -> A5 (880.0 Hz)
                    _connectWavBytes = GenerateTwoToneWav(587.33, 880.0, 0.085, 0.115, 0.22);
                }

                if (_disconnectWavBytes == null)
                {
                    // Soft descending two-tone chime: G5 (783.99 Hz) -> C5 (523.25 Hz)
                    _disconnectWavBytes = GenerateTwoToneWav(783.99, 523.25, 0.085, 0.115, 0.19);
                }

                // Write to assets/sounds next to executable if writable
                WriteSoundsToDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "sounds"));

                // Also write to %LocalAppData%\DNSParty\sounds as fallback
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                WriteSoundsToDirectory(Path.Combine(localAppData, "DNSParty", "sounds"));
            }
            catch
            {
                // Never throw if disk or audio initialization encounters an issue
            }
        }

        private void WriteSoundsToDirectory(string dirPath)
        {
            try
            {
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

                string connectPath = Path.Combine(dirPath, "connect.wav");
                if (!File.Exists(connectPath) && _connectWavBytes != null)
                {
                    File.WriteAllBytes(connectPath, _connectWavBytes);
                }

                string disconnectPath = Path.Combine(dirPath, "disconnect.wav");
                if (!File.Exists(disconnectPath) && _disconnectWavBytes != null)
                {
                    File.WriteAllBytes(disconnectPath, _disconnectWavBytes);
                }
            }
            catch
            {
                // Ignore read-only directory errors
            }
        }

        public void PlayConnect(bool enabled)
        {
            if (!enabled)
            {
                return;
            }

            PlayWavBytes(_connectWavBytes);
        }

        public void PlayDisconnect(bool enabled)
        {
            if (!enabled)
            {
                return;
            }

            PlayWavBytes(_disconnectWavBytes);
        }

        private static void PlayWavBytes(byte[] wavBytes)
        {
            if (wavBytes == null || wavBytes.Length == 0)
            {
                return;
            }

            try
            {
                var stream = new MemoryStream(wavBytes, false);
                var player = new SoundPlayer(stream);
                player.Play();
            }
            catch
            {
                // Never throw if audio hardware is unavailable
            }
        }

        private static byte[] GenerateTwoToneWav(double freq1, double freq2, double duration1Sec, double duration2Sec, double amplitude)
        {
            const int sampleRate = 44100;
            const short channels = 1;
            const short bitsPerSample = 16;

            int samples1 = (int)(sampleRate * duration1Sec);
            int samples2 = (int)(sampleRate * duration2Sec);
            int totalSamples = samples1 + samples2;
            int dataChunkSize = totalSamples * channels * (bitsPerSample / 8);

            using (var ms = new MemoryStream(44 + dataChunkSize))
            using (var writer = new BinaryWriter(ms))
            {
                // RIFF header
                writer.Write(new char[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataChunkSize);
                writer.Write(new char[] { 'W', 'A', 'V', 'E' });

                // fmt subchunk
                writer.Write(new char[] { 'f', 'm', 't', ' ' });
                writer.Write(16); // Subchunk1Size for PCM
                writer.Write((short)1); // AudioFormat 1 = PCM
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * (bitsPerSample / 8)); // ByteRate
                writer.Write((short)(channels * (bitsPerSample / 8))); // BlockAlign
                writer.Write(bitsPerSample);

                // data subchunk
                writer.Write(new char[] { 'd', 'a', 't', 'a' });
                writer.Write(dataChunkSize);

                // Tone 1
                for (int i = 0; i < samples1; i++)
                {
                    double t = (double)i / sampleRate;
                    double progress = (double)i / samples1;
                    double attack = Math.Min(1.0, i / (sampleRate * 0.006));
                    double decay = Math.Exp(-3.5 * progress);
                    double env = attack * decay;

                    double wave = Math.Sin(2.0 * Math.PI * freq1 * t) + 0.18 * Math.Sin(2.0 * Math.PI * (freq1 * 2.0) * t);
                    short sample = (short)(Math.Max(-1.0, Math.Min(1.0, wave * amplitude * env)) * short.MaxValue);
                    writer.Write(sample);
                }

                // Tone 2
                for (int i = 0; i < samples2; i++)
                {
                    double t = (double)i / sampleRate;
                    double progress = (double)i / samples2;
                    double attack = Math.Min(1.0, i / (sampleRate * 0.006));
                    double decay = Math.Exp(-4.2 * progress);
                    double env = attack * decay;

                    double wave = Math.Sin(2.0 * Math.PI * freq2 * t) + 0.15 * Math.Sin(2.0 * Math.PI * (freq2 * 2.0) * t);
                    short sample = (short)(Math.Max(-1.0, Math.Min(1.0, wave * amplitude * env)) * short.MaxValue);
                    writer.Write(sample);
                }

                writer.Flush();
                return ms.ToArray();
            }
        }
    }
}
