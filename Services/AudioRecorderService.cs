using NAudio.Wave;

namespace AISpeech.Services;

public sealed class AudioRecorderService : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _waveWriter;
    private MemoryStream? _memoryStream;
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
            _waveWriter.Write(e.Buffer, 0, e.BytesRecorded);
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

        // Dispose writer to finalize WAV header — NonClosingStream keeps _memoryStream open
        _waveWriter.Dispose();
        _waveWriter = null;

        _memoryStream.Position = 0;
        var result = _memoryStream;
        _memoryStream = null;
        return result;
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
