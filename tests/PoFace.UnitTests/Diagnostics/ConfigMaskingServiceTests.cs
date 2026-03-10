using PoFace.Api.Features.Diagnostics;

namespace PoFace.UnitTests.Diagnostics;

public sealed class ConfigMaskingServiceTests
{
    private readonly ConfigMaskingService _sut = new();

    [Fact]
    public void Mask_EightChars_ReturnsExpectedPattern()
    {
        _sut.Mask("ABCDWXYZ").Should().Be("ABCD****WXYZ");
    }

    [Fact]
    public void Mask_SixChars_ReturnsSafeOutput()
    {
        _sut.Mask("ABCDEF").Should().Be("****");
    }

    [Fact]
    public void Mask_Empty_ReturnsSafeOutput()
    {
        _sut.Mask(string.Empty).Should().Be("****");
    }
}
