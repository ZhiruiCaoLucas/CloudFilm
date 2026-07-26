using System.Text;
using System.Text.Json;
using CloudFilm.Infrastructure.Caching;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;

namespace CloudFilm.Api.Tests.Infrastructure;

public sealed class RedisCacheStoreTests
{
    private sealed record Payload(string StudyInstanceUid, int SeriesCount);

    private readonly IDistributedCache _cache = Substitute.For<IDistributedCache>();

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenEntryIsAbsent()
    {
        _cache.GetAsync("missing", Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        RedisCacheStore store = new(_cache);

        (await store.GetAsync<Payload>("missing")).Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_DeserialisesStoredPayload()
    {
        byte[] stored = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new Payload("1.2.840.113619.2.55", 4), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        _cache.GetAsync("study", Arg.Any<CancellationToken>()).Returns(stored);

        RedisCacheStore store = new(_cache);
        Payload? result = await store.GetAsync<Payload>("study");

        result.Should().NotBeNull();
        result!.StudyInstanceUid.Should().Be("1.2.840.113619.2.55");
        result.SeriesCount.Should().Be(4);
    }

    [Fact]
    public async Task SetAsync_AlwaysAppliesAnAbsoluteExpiry()
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
    public async Task SetAsync_RejectsNonPositiveTimeToLive(int seconds)
    {
        RedisCacheStore store = new(_cache);

        Func<Task> act = () => store.SetAsync("study", new Payload("1.2.3", 1), TimeSpan.FromSeconds(seconds));

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
