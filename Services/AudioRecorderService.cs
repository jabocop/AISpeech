using NAudio.Wave;

namespace AISpeech.Services;

public sealed class AudioRecorderService : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _waveWriter;
    private MemoryStream? _memoryStream;
    private readonly object _writeLock = new();
    private bool _disposed;

    public void StartRecording()
    {
        _memoryStream = new MemoryStream();
        var waveFormat = new WaveFormat(16000, 16, 1);

        _waveIn = new WaveInEvent
        {
            WaveFormat = waveFormat,
            BufferMilliseconds = 50
        };

        _waveWriter = new WaveFileWriter(new NonClosingStream(_memoryStream), waveFormat);

        _waveIn.DataAvailable += (_, e) =>
        {
            lock (_writeLock)
            {
                _waveWriter.Write(e.Buffer, 0, e.BytesRecorded);
            }
        };

        _waveIn.StartRecording();
    }

    public MemoryStream StopRecording()
    {
        if (_waveIn is null || _waveWriter is null || _memoryStream is null)
            throw new InvalidOperationException("Not currently recording.");

        _waveIn.StopRecording();
        _waveIn.Dispose();
        _waveIn = null;

        lock (_writeLock)
        {
            // Dispose writer to finalize WAV header — NonClosingStream keeps _memoryStream open
            _waveWriter.Dispose();
            _waveWriter = null;
        }

        _memoryStream.Position = 0;
        var result = _memoryStream;
        _memoryStream = null;
        return result;
    }

    /// <summary>
    /// Takes a snapshot of the current audio buffer as a valid WAV stream.
    /// Returns null if not currently recording or if there's not enough data.
    /// </summary>
    public MemoryStream? SnapshotBuffer()
    {
        byte[] pcmData;

        lock (_writeLock)
        {
            if (_waveWriter is null || _memoryStream is null)
                return null;

            _waveWriter.Flush();

            var streamLength = (int)_memoryStream.Position;
            // NAudio writes a header before PCM data — find the "data" chunk to extract raw PCM
            var buf = _memoryStream.GetBuffer();
            var dataOffset = FindDataChunkOffset(buf, streamLength);
            if (dataOffset < 0)
                return null;

            var pcmLength = streamLength - dataOffset;
            if (pcmLength <= 0)
                return null;

            pcmData = new byte[pcmLength];
            Array.Copy(buf, dataOffset, pcmData, 0, pcmLength);
        }

        // Build a clean minimal WAV (16kHz, 16-bit, mono) outside the lock
        var result = new MemoryStream();
        using (var writer = new WaveFileWriter(new NonClosingStream(result), new WaveFormat(16000, 16, 1)))
        {
            writer.Write(pcmData, 0, pcmData.Length);
        }
        result.Position = 0;
        return result;
    }

    /// <summary>
    /// Scans for the "data" marker in WAV header bytes and returns the offset
    /// where PCM samples begin (right after the "data" + size fields).
    /// </summary>
    private static int FindDataChunkOffset(byte[] buffer, int length)
    {
        // "data" = 0x64 0x61 0x74 0x61
        for (var i = 0; i <= length - 8; i++)
        {
            if (buffer[i] == 0x64 && buffer[i + 1] == 0x61 &&
                buffer[i + 2] == 0x74 && buffer[i + 3] == 0x61)
            {
                return i + 8; // skip "data" (4 bytes) + chunk size (4 bytes)
            }
        }
        return -1;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _waveIn?.Dispose();
        _waveWriter?.Dispose();
        _memoryStream?.Dispose();
    }

    /// <summary>
    /// Wraps a stream and prevents Dispose from closing the inner stream,
    /// so WaveFileWriter.Dispose() finalizes the WAV header without closing the MemoryStream.
    /// </summary>
    private sealed class NonClosingStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            // Intentionally do NOT dispose inner stream
        }
    }
}
