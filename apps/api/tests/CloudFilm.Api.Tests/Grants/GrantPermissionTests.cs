using CloudFilm.Application.Grants;
using FluentAssertions;

namespace CloudFilm.Api.Tests.Grants;

/// <summary>
/// What one permission is worth against another.
/// </summary>
/// <remarks>
/// The ladder is written in exactly one place, so this is where it is pinned
/// down. A change here is a change to who can reach a patient's imaging, and it
/// should be impossible to make one by accident.
/// </remarks>
public sealed class GrantPermissionTests
{
    private static readonly string[] _storedPermissions = ["View", "Download"];

    [Fact]
    public void ViewSatisfiesView() =>
        GrantPermissions.Satisfies(GrantPermissions.View, GrantPermissions.View).Should().BeTrue();

    /// <summary>
    /// Being allowed to look at a study is not being allowed to take a copy of
    /// it away.
    /// </summary>
    [Fact]
    public void ViewDoesNotSatisfyDownload() =>
        GrantPermissions.Satisfies(GrantPermissions.View, GrantPermissions.Download)
            .Should().BeFalse();

    /// <summary>
    /// Being allowed to take a copy and not to look at it is not a policy
    /// anybody meant to write.
    /// </summary>
    [Fact]
    public void DownloadSatisfiesView() =>
        GrantPermissions.Satisfies(GrantPermissions.Download, GrantPermissions.View)
            .Should().BeTrue();

    [Fact]
    public void DownloadSatisfiesDownload() =>
        GrantPermissions.Satisfies(GrantPermissions.Download, GrantPermissions.Download)
            .Should().BeTrue();

    /// <summary>
    /// A permission this application does not define permits nothing, whatever a
    /// row in the table says.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("All")]
    [InlineData("Admin")]
    [InlineData("view")]
    [InlineData("DOWNLOAD")]
    public void AnUnrecognisedPermissionSatisfiesNothing(string? held)
    {
        GrantPermissions.Satisfies(held, GrantPermissions.View).Should().BeFalse();
        GrantPermissions.Satisfies(held, GrantPermissions.Download).Should().BeFalse();
        GrantPermissions.IsRecognised(held).Should().BeFalse();
    }

    [Fact]
    public void NothingSatisfiesAnUnrecognisedRequirement()
    {
        GrantPermissions.Satisfies(GrantPermissions.Download, "All").Should().BeFalse();
        GrantPermissions.Satisfying("All").Should().BeEmpty();
    }

    [Fact]
    public void DownloadIsTheOnlyPermissionThatCoversBoth()
    {
        GrantPermissions.Satisfying(GrantPermissions.View)
            .Should().BeEquivalentTo(_storedPermissions);

        GrantPermissions.Satisfying(GrantPermissions.Download)
            .Should().Equal(GrantPermissions.Download);
    }

    /// <summary>
    /// Only two values are ever stored. A third meaning "all of them" would be a
    /// second spelling of Download that every query would have to match forever.
    /// </summary>
    [Fact]
    public void OnlyTwoPermissionsAreStorable()
    {
        GrantPermissions.All.Should().BeEquivalentTo(_storedPermissions);

        GrantPermissions.IsRecognised(GrantPermissions.View).Should().BeTrue();
        GrantPermissions.IsRecognised(GrantPermissions.Download).Should().BeTrue();
    }
}
