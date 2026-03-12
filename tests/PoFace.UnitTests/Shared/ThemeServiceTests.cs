using Microsoft.JSInterop;
using PoFace.Client.Services;

namespace PoFace.UnitTests.Shared;

public sealed class ThemeServiceTests
{
    [Fact]
    public async Task Initialize_CallsMaterialDarkOnce_AndInjectsVars()
    {
        var bridge = new Mock<IRadzenThemeBridge>();
        var js = new Mock<IJSRuntime>();

        js.Setup(j => j.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                It.IsAny<string>(), It.IsAny<object?[]?>()))
            .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
                Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>()));

        var sut = new ThemeService(bridge.Object, js.Object);

        await sut.InitializeAsync();
        await sut.InitializeAsync(); // second call should be a no-op

        bridge.Verify(b => b.SetTheme("material-dark"), Times.Once);
        js.Verify(j => j.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "setTerminalVars", It.IsAny<object?[]?>()), Times.Once);
    }
}
