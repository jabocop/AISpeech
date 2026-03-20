using AISpeech.Services;
using FluentAssertions;
using NAudio.Wave;
using Xunit;

namespace AISpeech.Tests.Services;

public class AudioRecorderServiceSnapshotTests
{
    [Fact]
    public void SnapshotBuffer_WhenNotRecording_ReturnsNull()
    {
        using var service = new AudioRecorderService();

        var snapshot = service.SnapshotBuffer();

        snapshot.Should().BeNull();
    }

    [Fact]
    public void SnapshotBuffer_WhileRecording_ReturnsValidWavStream()
    {
        using var service = new AudioRecorderService();
        service.StartRecording();

        // Wait for some audio data to accumulate
        Thread.Sleep(300);

        using var snapshot = service.SnapshotBuffer();

        snapshot.Should().NotBeNull();
        snapshot!.Position.Should().Be(0);
        snapshot.Length.Should().BeGreaterThan(44); // WAV header + some data

        // Verify it's a valid WAV by reading it with NAudio
        var reader = new WaveFileReader(snapshot);
        reader.WaveFormat.SampleRate.Should().Be(16000);
        reader.WaveFormat.BitsPerSample.Should().Be(16);
        reader.WaveFormat.Channels.Should().Be(1);
        reader.Length.Should().BeGreaterThan(0);

        service.StopRecording().Dispose();
    }

    [Fact]
    public void SnapshotBuffer_ProducesValidWavHeader()
    {
        using var service = new AudioRecorderService();
        service.StartRecording();

        Thread.Sleep(300);

        using var snapshot = service.SnapshotBuffer();
        snapshot.Should().NotBeNull();

        // Read the RIFF header
        var bytes = snapshot!.ToArray();
        // "RIFF" at offset 0
        bytes[0].Should().Be(0x52); // R
        bytes[1].Should().Be(0x49); // I
        bytes[2].Should().Be(0x46); // F
        bytes[3].Should().Be(0x46); // F

        // "WAVE" at offset 8
        bytes[8].Should().Be(0x57);  // W
        bytes[9].Should().Be(0x41);  // A
        bytes[10].Should().Be(0x56); // V
        bytes[11].Should().Be(0x45); // E

        // RIFF chunk size at offset 4 should be file size - 8
        var riffSize = BitConverter.ToInt32(bytes, 4);
        riffSize.Should().Be(bytes.Length - 8);

        service.StopRecording().Dispose();
    }

    [Fact]
    public void SnapshotBuffer_AfterStopRecording_ReturnsNull()
    {
        using var service = new AudioRecorderService();
        service.StartRecording();

        Thread.Sleep(200);
        service.StopRecording().Dispose();

        var snapshot = service.SnapshotBuffer();

        snapshot.Should().BeNull();
    }

    [Fact]
    public void SnapshotBuffer_MultipleSnapshots_AllReturnValidWav()
    {
        using var service = new AudioRecorderService();
        service.StartRecording();

        Thread.Sleep(200);
        using var snapshot1 = service.SnapshotBuffer();

        Thread.Sleep(200);
        using var snapshot2 = service.SnapshotBuffer();

        snapshot1.Should().NotBeNull();
        snapshot2.Should().NotBeNull();

        // Second snapshot should be larger (more audio accumulated)
        snapshot2!.Length.Should().BeGreaterThanOrEqualTo(snapshot1!.Length);

        // Both should be readable as WAV
        snapshot1.Position = 0;
        var reader1 = new WaveFileReader(snapshot1);
        reader1.WaveFormat.SampleRate.Should().Be(16000);

        snapshot2.Position = 0;
        var reader2 = new WaveFileReader(snapshot2);
        reader2.WaveFormat.SampleRate.Should().Be(16000);

        service.StopRecording().Dispose();
    }

    [Fact]
    public void SnapshotBuffer_DoesNotAffectFinalRecording()
    {
        using var service = new AudioRecorderService();
        service.StartRecording();

        Thread.Sleep(200);

        // Take a snapshot mid-recording
        using var snapshot = service.SnapshotBuffer();
        snapshot.Should().NotBeNull();

        Thread.Sleep(200);

        // Final recording should still work
        using var final = service.StopRecording();
        final.Should().NotBeNull();
        final.Position.Should().Be(0);
        final.Length.Should().BeGreaterThan(0);

        // Final should be a valid WAV
        var reader = new WaveFileReader(final);
        reader.WaveFormat.SampleRate.Should().Be(16000);

        // Final recording should be larger than the snapshot (more audio)
        final.Length.Should().BeGreaterThan(snapshot!.Length);
    }
}
