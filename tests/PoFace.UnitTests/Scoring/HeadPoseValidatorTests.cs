using PoFace.Api.Features.Scoring;

namespace PoFace.UnitTests.Scoring;

public sealed class HeadPoseValidatorTests
{
    [Fact]
    public void WhenYawAndPitchAreZero_ReturnsValid()
    {
        HeadPoseValidator.Validate(0, 0).Should().BeTrue();
    }

    [Fact]
    public void WhenYawExceedsBoundary_ReturnsInvalid()
    {
        HeadPoseValidator.Validate(21, 0).Should().BeFalse();
    }

    [Fact]
    public void WhenPitchNegativeExceedsBoundary_ReturnsInvalid()
    {
        HeadPoseValidator.Validate(0, -21).Should().BeFalse();
    }

    [Fact]
    public void WhenYawAndPitchAtBoundary_ReturnsValid()
    {
        // Boundary is inclusive: |yaw| <= 20 && |pitch| <= 20
        HeadPoseValidator.Validate(20, 20).Should().BeTrue();
    }

    [Fact]
    public void WhenYawNegativeAtBoundary_ReturnsValid()
    {
        HeadPoseValidator.Validate(-20, 0).Should().BeTrue();
    }

    [Fact]
    public void WhenPitchJustOverBoundary_ReturnsInvalid()
    {
        HeadPoseValidator.Validate(0, 20.1).Should().BeFalse();
    }
}
