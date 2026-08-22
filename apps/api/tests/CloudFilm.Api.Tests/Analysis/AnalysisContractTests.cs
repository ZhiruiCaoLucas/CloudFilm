using System.Reflection;
using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Analysis;
using CloudFilm.Application.Grants;
using CloudFilm.Application.Imaging;
using CloudFilm.Application.ReadState;
using CloudFilm.Application.Sharing;
using FluentAssertions;

namespace CloudFilm.Api.Tests.Analysis;

/// <summary>
/// The shape of an analysis, and of the port that produces one.
/// </summary>
/// <remarks>
/// Nothing here exercises a model, because there is not one yet. What it pins
/// down is the contract every later piece will be written against - what an
/// analyser may be given, and what a result may carry - while both are still
/// cheap to change.
/// </remarks>
public sealed class AnalysisContractTests
{
    /// <summary>
    /// Every feature that stores a Study Instance UID uses the same bound. A
    /// second number here would be a truncation waiting to happen, and a
    /// truncated UID is a different study.
    /// </summary>
    [Fact]
    public void AStudyIdentifierIsBoundedLikeEveryOtherOne()
    {
        AnalysisLimits.MaxStudyInstanceUidLength.Should().Be(128);

        AnalysisLimits.MaxStudyInstanceUidLength
            .Should().Be(StudyReadStateLimits.MaxStudyInstanceUidLength)
            .And.Be(GrantLimits.MaxStudyInstanceUidLength)
            .And.Be(ShareLinkLimits.MaxStudyInstanceUidLength);
    }

    /// <summary>
    /// A confidence is a proportion. A contract that leaves the range unsaid
    /// leaves every renderer to guess whether 85 means "high" or "eighty-five
    /// times as likely as certain".
    /// </summary>
    [Fact]
    public void AConfidenceRunsFromZeroToOne()
    {
        AnalysisLimits.MinimumScore.Should().Be(0d);
        AnalysisLimits.MaximumScore.Should().Be(1d);
    }

    /// <summary>
    /// A ceiling exists, so that a misbehaving model cannot turn one study into a
    /// response nobody can render.
    /// </summary>
    [Fact]
    public void ThereIsACeilingOnHowMuchOneAnalysisMayReport() =>
        AnalysisLimits.MaxFindings.Should().BePositive();

    /// <summary>
    /// The result must not become a second place patient identity lives.
    /// </summary>
    /// <remarks>
    /// The analyser is told which study it is looking at and nothing about whose
    /// it is. If a name or a record number ever appears on this record it will
    /// travel to every client that renders a finding, and the PHI boundary this
    /// milestone drew will have moved without anybody deciding to move it.
    /// </remarks>
    [Fact]
    public void AnAnalysisCarriesNoPatientIdentity()
    {
        string[] expected =
        [
            "StudyInstanceUid",
            "ModelName",
            "ModelVersion",
            "CompletedAtUtc",
            "Findings",
        ];

        string[] properties = typeof(StudyAnalysis)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        properties.Should().BeEquivalentTo(expected);
    }

    /// <summary>
    /// A finding is a label and a number. Regions and outlines are absent on
    /// purpose: promising geometry nothing produces is worse than not mentioning
    /// it.
    /// </summary>
    [Fact]
    public void AFindingIsALabelAndAScore()
    {
        string[] expected = ["Label", "Score"];

        typeof(AnalysisFinding)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Should().BeEquivalentTo(expected);
    }

    /// <summary>
    /// A result names the model that produced it, so two runs can be told apart
    /// and a model found to be wrong can be traced to what it said.
    /// </summary>
    [Fact]
    public void TwoResultsFromDifferentModelVersionsAreNotEqual()
    {
        DateTimeOffset completed = new(2026, 8, 16, 9, 0, 0, TimeSpan.Zero);
        AnalysisFinding[] findings = [new("example", 0.5d)];

        StudyAnalysis first = new("1.2.840.113564.1", "example-model", "1.0.0", completed, findings);
        StudyAnalysis second = first with { ModelVersion = "1.0.1" };

        second.Should().NotBe(first);
        (first with { }).Should().Be(first);
    }

    /// <summary>
    /// The security property of the whole milestone: an analyser is handed bytes,
    /// never an address.
    /// </summary>
    /// <remarks>
    /// A parameter that named a study to fetch, or carried a URL, would make the
    /// analyser able to reach imaging nobody authorised - and would need its own
    /// answer to "may this caller see it", which is the question
    /// <c>IStudyAccessPolicy</c> already answers, before the download source is
    /// asked for anything. This test fails the moment that shape changes.
    /// </remarks>
    [Fact]
    public void AnAnalyserIsGivenAnOpenStudyAndNoWayToFetchAnother()
    {
        MethodInfo[] methods = typeof(IStudyAnalyser).GetMethods();

        methods.Should().ContainSingle("one port, one question");

        Type[] expected = [typeof(string), typeof(StudyDownload), typeof(CancellationToken)];

        Type[] parameters = methods[0].GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        parameters.Should().Equal(expected);
    }
}
