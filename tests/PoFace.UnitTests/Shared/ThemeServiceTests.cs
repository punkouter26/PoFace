using Microsoft.JSInterop;
using PoFace.Client.Shared;

namespace PoFace.UnitTests.Shared;

public sealed class ThemeServiceTests
{
    [Fact]
    public async Task Initialize_CallsMaterialDarkOnce_AndInjectsVars()
    {
        var bridge = new Mock<IRadzenThemeBridge>();
        var js = new Mock<IJSRuntime>();
        string capturedScript = string.Empty;

                js.Setup(j => j.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(It.IsAny<string>(), It.IsAny<object?[]?>()))
          .Callback<string, object?[]?>((identifier, args) =>
          {
              if (identifier == "eval" && args is not null && args.Length == 1)
              {
                  capturedScript = args[0]?.ToString() ?? string.Empty;
              }
          })
                    .Returns(new ValueTask<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(Mock.Of<Microsoft.JSInterop.Infrastructure.IJSVoidResult>()));

        var sut = new ThemeService(bridge.Object, js.Object);

        await sut.InitializeAsync();
        await sut.InitializeAsync();

        bridge.Verify(b => b.SetTheme("material-dark"), Times.Once);
        capturedScript.Should().Contain("--color-primary', '#00ff00");
        capturedScript.Should().Contain("--color-bg', '#0a0a0a");
    }
}
