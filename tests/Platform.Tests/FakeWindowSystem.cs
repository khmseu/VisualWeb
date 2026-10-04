using VisualWeb.Platform.Abstractions;

namespace VisualWeb.Platform.Tests;

internal sealed class FakeWindowSystem : IWindowSystem
{
    private readonly Dictionary<WindowId, FakeWindow> windows = [];
    private readonly Queue<WindowEvent> events = [];
    private uint nextId;
    private bool disposed;

    public string Backend => "fake";
    public event Action? QuitRequested;

    public IPlatformWindow CreateWindow(WindowOptions options)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var window = new FakeWindow(new(++nextId), new(options.Width, options.Height), id => windows.Remove(id));
        windows.Add(window.Id, window);
        return window;
    }

    public void Enqueue(WindowEvent windowEvent) => events.Enqueue(windowEvent);
    public void RequestQuit() => QuitRequested?.Invoke();

    public void PumpEvents()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        while (events.TryDequeue(out var windowEvent))
        {
            if (windows.TryGetValue(windowEvent.Window, out var window))
            {
                window.Dispatch(windowEvent);
            }
        }
    }

    public void Dispose()
    {
        foreach (var window in windows.Values.ToArray())
        {
            window.Dispose();
        }

        disposed = true;
    }

    internal sealed class FakeWindow(WindowId id, PixelSize size, Action<WindowId> remove) : IPlatformWindow, IPixelSurface
    {
        private bool disposed;
        public WindowId Id => id;
        public PixelSize LogicalSize => size;
        public PixelSize PixelSize => size;
        public float DisplayScale => 1;
        public float PixelDensity => 1;
        public IPixelSurface Surface => this;
        public byte[]? LastFrame { get; private set; }
        public event Action<WindowEvent>? EventReceived;
        public void SetTitle(string title) => ObjectDisposedException.ThrowIf(disposed, this);
        public void SetTextInput(bool enabled) => ObjectDisposedException.ThrowIf(disposed, this);
        public void Dispatch(WindowEvent windowEvent) => EventReceived?.Invoke(windowEvent);

        public void Present(ReadOnlySpan<byte> pixels, PixelSize frameSize, int stride)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            PixelBuffer.Validate(pixels.Length, frameSize, stride);
            if (frameSize != size)
            {
                throw new ArgumentException("Frame size does not match window.");
            }

            LastFrame = pixels.ToArray();
        }

        public void Dispose()
        {
            remove(Id);
            disposed = true;
        }
    }
}
