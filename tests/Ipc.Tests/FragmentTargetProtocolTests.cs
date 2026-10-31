using System.Globalization;
using System.Text;
using System.Text.Json;
using VisualWeb.Ipc.Contracts;
using Xunit;

namespace VisualWeb.Ipc.Tests;

public sealed class FragmentTargetProtocolTests
{
    [Fact]
    public void FragmentTargetCountHasAnExactLimit()
    {
        var exact = Enumerable.Range(0, RendererProtocol.MaxFragmentTargets)
            .Select(index => new PageFragmentTarget(index.ToString(CultureInfo.InvariantCulture), 0)).ToArray();
        RendererProtocol.ValidateFragmentTargets(exact, 0);
        Assert.Throws<IpcProtocolException>(() => RendererProtocol.ValidateFragmentTargets(
            [.. exact, new PageFragmentTarget("one-over", 0)], 0));
    }

    [Fact]
    public void FragmentMetadataChecksExactSerializedJsonBytesAndUtf8Bytes()
    {
        var exact = Enumerable.Range(0, 128).Select(index => new PageFragmentTarget("", index)).ToArray();
        var remaining = RendererProtocol.MaxFragmentMetadataBytes - JsonSerializer.SerializeToUtf8Bytes(exact).Length;
        for (var index = 0; index < exact.Length; index++)
        {
            var length = Math.Min(RendererProtocol.MaxTextCharacters, remaining);
            Assert.True(length > 0);
            exact[index] = exact[index] with { Id = new string('x', length) };
            remaining -= length;
        }
        Assert.Equal(0, remaining);
        Assert.Equal(RendererProtocol.MaxFragmentMetadataBytes, JsonSerializer.SerializeToUtf8Bytes(exact).Length);
        Assert.True(exact.Sum(target => Encoding.UTF8.GetByteCount(target.Id)) < RendererProtocol.MaxFragmentMetadataBytes);
        RendererProtocol.ValidateFragmentTargets(exact, 127);

        var oneOver = exact.ToArray();
        oneOver[^1] = oneOver[^1] with { Id = oneOver[^1].Id + "x" };
        Assert.Equal(RendererProtocol.MaxFragmentMetadataBytes + 1, JsonSerializer.SerializeToUtf8Bytes(oneOver).Length);
        var jsonFailure = Assert.Throws<IpcProtocolException>(() => RendererProtocol.ValidateFragmentTargets(oneOver, 127));
        Assert.Equal("Renderer fragment target metadata byte limit exceeded. Serialized JSON bytes.", jsonFailure.Message);

        var utf8AtLimit = Enumerable.Repeat(
            new PageFragmentTarget(new string('\u00e9', RendererProtocol.MaxTextCharacters), 0), 64).ToArray();
        Assert.Equal(RendererProtocol.MaxFragmentMetadataBytes, utf8AtLimit.Sum(target => Encoding.UTF8.GetByteCount(target.Id)));
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(utf8AtLimit).Length > RendererProtocol.MaxFragmentMetadataBytes);
        var utf8Exact = Assert.Throws<IpcProtocolException>(() => RendererProtocol.ValidateFragmentTargets(utf8AtLimit, 0));
        Assert.Equal("Renderer fragment target metadata byte limit exceeded. Serialized JSON bytes.", utf8Exact.Message);
        var utf8Over = utf8AtLimit.Append(new PageFragmentTarget("a", 0)).ToArray();
        Assert.Equal(RendererProtocol.MaxFragmentMetadataBytes + 1, utf8Over.Sum(target => Encoding.UTF8.GetByteCount(target.Id)));
        var utf8Failure = Assert.Throws<IpcProtocolException>(() => RendererProtocol.ValidateFragmentTargets(utf8Over, 0));
        Assert.Equal("Renderer fragment target metadata byte limit exceeded. UTF-8 bytes.", utf8Failure.Message);

        var escaped = Enumerable.Repeat(new PageFragmentTarget(new string('<', RendererProtocol.MaxTextCharacters), 0), 22).ToArray();
        Assert.True(escaped.Sum(target => Encoding.UTF8.GetByteCount(target.Id)) < RendererProtocol.MaxFragmentMetadataBytes);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(escaped).Length > RendererProtocol.MaxFragmentMetadataBytes);
        var escapedFailure = Assert.Throws<IpcProtocolException>(() => RendererProtocol.ValidateFragmentTargets(escaped, 0));
        Assert.Equal("Renderer fragment target metadata byte limit exceeded. Serialized JSON bytes.", escapedFailure.Message);
    }
}
