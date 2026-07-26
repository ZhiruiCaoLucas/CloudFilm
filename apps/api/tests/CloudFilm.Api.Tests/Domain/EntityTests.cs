using CloudFilm.Domain.Common;
using FluentAssertions;

namespace CloudFilm.Api.Tests.Domain;

public sealed class EntityTests
{
    private sealed class Study(Guid id) : Entity<Guid>(id);

    private sealed class Series(Guid id) : Entity<Guid>(id);

    [Fact]
    public void Entities_WithSameTypeAndId_AreEqual()
    {
        Guid id = Guid.NewGuid();

        new Study(id).Should().Be(new Study(id));
    }

    [Fact]
    public void Entities_WithDifferentIds_AreNotEqual()
    {
        new Study(Guid.NewGuid()).Should().NotBe(new Study(Guid.NewGuid()));
    }

    [Fact]
    public void Entities_OfDifferentTypes_AreNotEqual()
    {
        Guid id = Guid.NewGuid();

        new Study(id).Equals((object)new Series(id)).Should().BeFalse();
    }

    [Fact]
    public void HashCode_IsStable_ForSameIdentity()
    {
        Guid id = Guid.NewGuid();

        new Study(id).GetHashCode().Should().Be(new Study(id).GetHashCode());
    }
}
