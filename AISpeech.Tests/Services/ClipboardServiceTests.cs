using AISpeech.Services;
using FluentAssertions;
using Xunit;

namespace AISpeech.Tests.Services;

public class ClipboardServiceTests
{
    /// <summary>
    /// Runs an action on a dedicated STA thread with a WinForms message pump.
    /// BeginInvoke requires messages to be pumped, so we spin on Application.DoEvents().
    /// </summary>
    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(10));
        if (exception is not null)
            throw new AggregateException(exception);
    }

    /// <summary>
    /// Awaits a task by pumping the WinForms message queue until it completes.
    /// </summary>
    private static void PumpUntilComplete(Task task, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!task.IsCompleted)
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Task did not complete within the timeout period.");
            Application.DoEvents();
            Thread.Sleep(1);
        }
        task.GetAwaiter().GetResult(); // rethrow if faulted
    }

    private static (ClipboardService Service, Form MarshalForm) CreateService(
        ISystemInputSimulator? inputSimulator = null)
    {
        var form = new Form { ShowInTaskbar = false, WindowState = FormWindowState.Minimized };
        form.Show();
        form.Hide();
        // Prime the message pump so WindowsFormsSynchronizationContext is established
        Application.DoEvents();
        return (new ClipboardService(form, inputSimulator), form);
    }

    /// <summary>
    /// Test stub that records calls without touching real OS APIs.
    /// </summary>
    private sealed class FakeInputSimulator : ISystemInputSimulator
    {
        public IntPtr LastForegroundWindow { get; private set; }
        public int ForceForegroundWindowCallCount { get; private set; }
        public int SimulateCtrlVCallCount { get; private set; }

        public void ForceForegroundWindow(IntPtr hWnd)
        {
            LastForegroundWindow = hWnd;
            ForceForegroundWindowCallCount++;
        }

        public void SimulateCtrlV()
        {
            SimulateCtrlVCallCount++;
        }
    }

    // --- CaptureTargetWindow ---

    [Fact]
    public void CaptureTargetWindow_ReturnsNonZeroHandle()
    {
        var handle = ClipboardService.CaptureTargetWindow();

        handle.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void CaptureTargetWindow_ReturnsSameWindow_WhenCalledTwiceQuickly()
    {
        var first = ClipboardService.CaptureTargetWindow();
        var second = ClipboardService.CaptureTargetWindow();

        first.Should().Be(second);
    }

    // --- SetTextAsync (clipboard-only, no auto-paste) ---

    [Fact]
    public void SetTextAsync_CopiesTextToClipboard()
    {
        RunOnStaThread(() =>
        {
            var (service, form) = CreateService();
            try
            {
                var task = service.SetTextAsync("hello clipboard");
                PumpUntilComplete(task, TimeSpan.FromSeconds(5));

                Clipboard.GetText().Should().Be("hello clipboard");
            }
            finally
            {
                form.Dispose();
            }
        });
    }

    [Fact]
    public void SetTextAsync_OverwritesPreviousClipboardContent()
    {
        RunOnStaThread(() =>
        {
            var (service, form) = CreateService();
            try
            {
                var task1 = service.SetTextAsync("first");
                PumpUntilComplete(task1, TimeSpan.FromSeconds(5));
                Clipboard.GetText().Should().Be("first");

                var task2 = service.SetTextAsync("second");
                PumpUntilComplete(task2, TimeSpan.FromSeconds(5));
                Clipboard.GetText().Should().Be("second");
            }
            finally
            {
                form.Dispose();
            }
        });
    }

    // --- SetTextAsync with autoPaste=true, but zero window handle (clipboard-only path) ---

    [Fact]
    public void SetTextAsync_AutoPasteTrue_ZeroWindow_CopiesWithoutCrashing()
    {
        RunOnStaThread(() =>
        {
            var (service, form) = CreateService();
            try
            {
                var task = service.SetTextAsync("safe text", autoPaste: true, targetWindow: IntPtr.Zero);
                PumpUntilComplete(task, TimeSpan.FromSeconds(5));

                Clipboard.GetText().Should().Be("safe text");
            }
            finally
            {
                form.Dispose();
            }
        });
    }

    // --- SetTextAsync with autoPaste=false and a window handle (should NOT paste) ---

    [Fact]
    public void SetTextAsync_AutoPasteFalse_WithWindow_OnlyCopies()
    {
        RunOnStaThread(() =>
        {
            var (service, form) = CreateService();
            try
            {
                var task = service.SetTextAsync("clipboard only", autoPaste: false, targetWindow: form.Handle);
                PumpUntilComplete(task, TimeSpan.FromSeconds(5));

                Clipboard.GetText().Should().Be("clipboard only");
            }
            finally
            {
                form.Dispose();
            }
        });
    }

    // --- SetTextAsync with autoPaste=true and a real window handle (full paste path) ---

    [Fact]
    public void SetTextAsync_AutoPasteTrue_WithWindow_CopiesAndDoesNotThrow()
    {
        RunOnStaThread(() =>
        {
            var fake = new FakeInputSimulator();
            var (service, form) = CreateService(fake);
            try
            {
                var task = service.SetTextAsync("pasted text", autoPaste: true, targetWindow: form.Handle);
                PumpUntilComplete(task, TimeSpan.FromSeconds(5));

                Clipboard.GetText().Should().Be("pasted text");
                fake.ForceForegroundWindowCallCount.Should().Be(1);
                fake.LastForegroundWindow.Should().Be(form.Handle);
                fake.SimulateCtrlVCallCount.Should().Be(1);
            }
            finally
            {
                form.Dispose();
            }
        });
    }

    // --- Verify auto-paste is NOT invoked when autoPaste=false ---

    [Fact]
    public void SetTextAsync_AutoPasteFalse_DoesNotCallInputSimulator()
    {
        RunOnStaThread(() =>
        {
            var fake = new FakeInputSimulator();
            var (service, form) = CreateService(fake);
            try
            {
                var task = service.SetTextAsync("no paste", autoPaste: false, targetWindow: form.Handle);
                PumpUntilComplete(task, TimeSpan.FromSeconds(5));

                fake.ForceForegroundWindowCallCount.Should().Be(0);
                fake.SimulateCtrlVCallCount.Should().Be(0);
            }
            finally
            {
                form.Dispose();
            }
        });
    }

    // --- Verify auto-paste is NOT invoked when window is zero ---

    [Fact]
    public void SetTextAsync_AutoPasteTrue_ZeroWindow_DoesNotCallInputSimulator()
    {
        RunOnStaThread(() =>
        {
            var fake = new FakeInputSimulator();
            var (service, form) = CreateService(fake);
            try
            {
                var task = service.SetTextAsync("no paste", autoPaste: true, targetWindow: IntPtr.Zero);
                PumpUntilComplete(task, TimeSpan.FromSeconds(5));

                fake.ForceForegroundWindowCallCount.Should().Be(0);
                fake.SimulateCtrlVCallCount.Should().Be(0);
            }
            finally
            {
                form.Dispose();
            }
        });
    }
}
