using Microsoft.JSInterop;
using PoFace.Client.Services;

namespace PoFace.UnitTests.Audio;

public sealed class AudioServiceTests
{
    [Fact]
    public async Task PlayMethods_InvokeExpectedJsFunctions()
    {
        var js = new Mock<IJSRuntime>();
        js.Setup(j => j.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                It.IsAny<string>(),
                It.IsAny<object?[]?>()))
          .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>()));

        var sut = new AudioService(js.Object);

        await sut.PlayBlipAsync(3);
        await sut.PlayShutterAsync();
        await sut.PlaySuccessChimeAsync();
        await sut.VibrateDeviceAsync([80]);

        js.Verify(j => j.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("audioInterop.playBlip", It.IsAny<object?[]?>()), Times.Once);
        js.Verify(j => j.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("audioInterop.playShutter", It.IsAny<object?[]?>()), Times.Once);
        js.Verify(j => j.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("audioInterop.playSuccessChime", It.IsAny<object?[]?>()), Times.Once);
        js.Verify(j => j.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("audioInterop.vibrateDevice", It.IsAny<object?[]?>()), Times.Once);
    }

    [Fact]
    public async Task JsException_IsSwallowed()
    {
        var js = new Mock<IJSRuntime>();
        js.Setup(j => j.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(It.IsAny<string>(), It.IsAny<object?[]?>()))
          .Throws(new JSException("boom"));

        var sut = new AudioService(js.Object);

        await sut.PlayBlipAsync(1);
        await sut.PlayShutterAsync();
        await sut.PlaySuccessChimeAsync();
        await sut.VibrateDeviceAsync([200]);
    }
}
