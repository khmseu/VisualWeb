using System.Runtime.InteropServices;
using SDL3;
using VisualWeb.Platform.Abstractions;

namespace VisualWeb.Platform.Sdl;

internal sealed class SdlWindow(SdlWindowSystem owner, IntPtr handle, WindowId id) : IPlatformWindow, IPixelSurface
{
    private bool disposed;
    public WindowId Id { get; } = id;
    public IPixelSurface Surface => this;
    public event Action<WindowEvent>? EventReceived;

    public PixelSize LogicalSize
    {
        get
        {
            EnsureAccess();
            if (!SDL.GetWindowSize(handle, out var width, out var height))
            {
                throw SdlWindowSystem.Error("Read logical window size");
            }

            return new(width, height);
        }
    }

    public float PixelDensity
    {
        get
        {
            EnsureAccess();
            var density = SDL.GetWindowPixelDensity(handle);
            if (density <= 0)
            {
                throw SdlWindowSystem.Error("Read pixel density");
            }

            return density;
        }
    }

    public PixelSize PixelSize
    {
        get
        {
            EnsureAccess();
            if (!SDL.GetWindowSizeInPixels(handle, out var width, out var height))
            {
                throw SdlWindowSystem.Error("Read window pixel size");
            }

            return new(width, height);
        }
    }

    public float DisplayScale
    {
        get
        {
            EnsureAccess();
            var scale = SDL.GetWindowDisplayScale(handle);
            if (scale <= 0)
            {
                throw SdlWindowSystem.Error("Read display scale");
            }

            return scale;
        }
    }

    public void SetTitle(string title)
    {
        EnsureAccess();
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (!SDL.SetWindowTitle(handle, title))
        {
            throw SdlWindowSystem.Error("Set window title");
        }
    }

    public void SetTextInput(bool enabled)
    {
        EnsureAccess();
        if (!(enabled ? SDL.StartTextInput(handle) : SDL.StopTextInput(handle)))
        {
            throw SdlWindowSystem.Error("Set text input");
        }
    }

    public void SetClipboardText(string text)
    {
        EnsureAccess();
        ArgumentNullException.ThrowIfNull(text);
        if (!SDL.SetClipboardText(text)) { throw SdlWindowSystem.Error("Set clipboard text"); }
    }

    public void Present(ReadOnlySpan<byte> pixels, PixelSize size, int stride)
    {
        EnsureAccess();
        PixelBuffer.Validate(pixels.Length, size, stride);
        if (size != PixelSize)
        {
            throw new ArgumentException("Frame size must match the current window pixel size.", nameof(size));
        }

        var destination = SDL.GetWindowSurface(handle);
        if (destination == IntPtr.Zero)
        {
            throw SdlWindowSystem.Error("Get window surface");
        }

        // SDL uses packed integer names; little-endian ARGB8888 is BGRA byte order.
        var format = BitConverter.IsLittleEndian ? SDL.PixelFormat.ARGB8888 : SDL.PixelFormat.BGRA8888;
        var pinned = GCHandle.Alloc(pixels.ToArray(), GCHandleType.Pinned);
        var source = IntPtr.Zero;
        try
        {
            source = SDL.CreateSurfaceFrom(size.Width, size.Height, format, pinned.AddrOfPinnedObject(), stride);
            if (source == IntPtr.Zero)
            {
                throw SdlWindowSystem.Error("Create frame surface");
            }

            if (!SDL.SetSurfaceBlendMode(source, SDL.BlendMode.None)
                || !SDL.BlitSurface(source, IntPtr.Zero, destination, IntPtr.Zero)
                || !SDL.UpdateWindowSurface(handle))
            {
                throw SdlWindowSystem.Error("Present pixels");
            }
        }
        finally
        {
            if (source != IntPtr.Zero)
            {
                SDL.DestroySurface(source);
            }

            pinned.Free();
        }
    }

    internal void Dispatch(WindowEvent windowEvent) => EventReceived?.Invoke(windowEvent);

    private void EnsureAccess()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        owner.EnsureAccess();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        owner.EnsureAccess();
        SDL.DestroyWindow(handle);
        owner.Remove(Id);
        disposed = true;
    }
}
