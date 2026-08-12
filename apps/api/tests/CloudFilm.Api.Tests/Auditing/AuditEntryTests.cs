using System.Reflection;
using CloudFilm.Application.Auditing;
using FluentAssertions;

namespace CloudFilm.Api.Tests.Auditing;

/// <summary>
/// What the audit contract can and cannot carry.
/// </summary>
/// <remarks>
/// These assert the shape rather than behaviour, because the shape is the
/// control. A password or a token cannot be audited by accident if there is
/// nowhere to put one, and these fail the build the moment somebody adds a
/// field that would make it possible.
/// </remarks>
public sealed class AuditEntryTests
{
    private static readonly string[] _forbiddenFragments =
    [
        "password", "passwd", "secret", "token", "jwt", "bearer", "signingkey",
        "credential", "hash", "body", "payload", "name", "birth", "dob", "gender",
    ];

    [Fact]
    public void AuditEntryCarriesOnlyTheFourDescribedFields()
    {
        string[] properties = typeof(AuditEntry)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Where(name => name != "EqualityContract")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        properties.Should().BeEquivalentTo(
            ["ActorUserId", "Action", "ResourceId", "ResourceType"],
            "the audit contract must not grow a field that could hold a credential or PHI");
    }

    [Fact]
    public void AuditEntryHasNoFieldThatCouldHoldACredentialOrPatientIdentity()
    {
        IEnumerable<string> names = typeof(AuditEntry)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name.ToLowerInvariant());

        foreach (string name in names)
        {
            foreach (string forbidden in _forbiddenFragments)
            {
                name.Should().NotContain(forbidden);
            }
        }
    }

    [Fact]
    public void ActorUserIdIsOptionalSoAFailedSignInCanBeRecordedWithoutAnActor()
    {
        AuditEntry entry = new(null, AuditAction.LoginFailed, AuditResourceType.Authentication, null);

        entry.ActorUserId.Should().BeNull();
    }

    [Fact]
    public void ActionsUseTheAgreedConstantValues()
    {
        AuditAction.LoginSucceeded.Should().Be("LOGIN_SUCCEEDED");
        AuditAction.LoginFailed.Should().Be("LOGIN_FAILED");
        AuditAction.ViewPatient.Should().Be("VIEW_PATIENT");
        AuditAction.ViewStudy.Should().Be("VIEW_STUDY");
    }

    [Fact]
    public void ResourceTypesUseTheAgreedConstantValues()
    {
        AuditResourceType.Authentication.Should().Be("Authentication");
        AuditResourceType.Patient.Should().Be("Patient");
        AuditResourceType.Study.Should().Be("Study");
    }
}
