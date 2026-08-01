using CloudFilm.Retrieve.Hospital;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CloudFilm.Retrieve.Tests;

public sealed class HospitalAdapterRegistryTests
{
    private sealed class StubAdapter(string code) : IHospitalAdapter
    {
        public string HospitalCode { get; } = code;

        public string DisplayName => $"Hospital {HospitalCode}";

        public Task<HospitalConnectivity> ProbeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(HospitalConnectivity.Reachable(TimeSpan.Zero));

        // These tests cover registration and resolution, not querying, so the
        // query members return empty results rather than being exercised.
        public Task<IReadOnlyList<PatientSummary>> ListPatientsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PatientSummary>>([]);

        public Task<PatientRecord?> FindPatientAsync(
            string medicalRecordNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PatientRecord?>(null);

        public Task<StudyContext?> FindStudyAsync(
            string studyInstanceUid,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<StudyContext?>(null);
    }

    private static IHospitalAdapterRegistry Build(params IHospitalAdapter[] adapters)
    {
        ServiceCollection services = new();
        services.AddHospitalAdapters();

        foreach (IHospitalAdapter adapter in adapters)
        {
            services.AddSingleton(adapter);
        }

        return services.BuildServiceProvider().GetRequiredService<IHospitalAdapterRegistry>();
    }

    [Fact]
    public void RegistryIsEmptyWhenNoAdapterIsRegistered()
    {
        Build().Adapters.Should().BeEmpty();
    }

    [Fact]
    public void FindResolvesAdapterCaseInsensitively()
    {
        IHospitalAdapterRegistry registry = Build(new StubAdapter("STMARY"));

        registry.Find("stmary").Should().NotBeNull();
        registry.Find("StMary")!.DisplayName.Should().Be("Hospital STMARY");
    }

    [Fact]
    public void FindReturnsNullForUnknownHospital()
    {
        Build(new StubAdapter("STMARY")).Find("UNKNOWN").Should().BeNull();
    }

    [Fact]
    public void RegistryRejectsDuplicateHospitalCodes()
    {
        Action act = () => Build(new StubAdapter("STMARY"), new StubAdapter("stmary"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*More than one hospital adapter*");
    }
}
