using System.Text;
using System.Text.Json;
using CloudFilm.Infrastructure.Caching;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;

namespace CloudFilm.Api.Tests.Infrastructure;

public sealed class RedisCacheStoreTests
{
    // Cached rather than constructed per call: JsonSerializerOptions is expensive
    // to build and caching it is what the production RedisCacheStore does, so the
    // test serialises exactly the way the code under test deserialises.
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    private sealed record Payload(string StudyInstanceUid, int SeriesCount);

    private readonly IDistributedCache _cache = Substitute.For<IDistributedCache>();

    [Fact]
    public async Task GetAsyncReturnsNullWhenEntryIsAbsent()
    {
        _cache.GetAsync("missing", Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        RedisCacheStore store = new(_cache);

        (await store.GetAsync<Payload>("missing")).Should().BeNull();
    }

    [Fact]
    public async Task GetAsyncDeserialisesStoredPayload()
    {
        byte[] stored = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new Payload("1.2.840.113619.2.55", 4), _serializerOptions));
        _cache.GetAsync("study", Arg.Any<CancellationToken>()).Returns(stored);

        RedisCacheStore store = new(_cache);
        Payload? result = await store.GetAsync<Payload>("study");

        result.Should().NotBeNull();
        result!.StudyInstanceUid.Should().Be("1.2.840.113619.2.55");
        result.SeriesCount.Should().Be(4);
    }

    [Fact]
    public async Task SetAsyncAlwaysAppliesAnAbsoluteExpiry()
    {
        RedisCacheStore store = new(_cache);

        await store.SetAsync("study", new Payload("1.2.3", 1), TimeSpan.FromMinutes(15));

        await _cache.Received(1).SetAsync(
            "study",
            Arg.Any<byte[]>(),
            Arg.Is<DistributedCacheEntryOptions>(options =>
                options.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(15)),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public async Task SetAsyncRejectsNonPositiveTimeToLive(int seconds)
    {
        RedisCacheStore store = new(_cache);

        Func<Task> act = () => store.SetAsync("study", new Payload("1.2.3", 1), TimeSpan.FromSeconds(seconds));

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
