using CloudFilm.Application.Abstractions;
using CloudFilm.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CloudFilm.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps creation and modification timestamps on every <see cref="IAuditableEntity"/>
/// as it is written.
/// </summary>
/// <remarks>
/// Centralising this in an interceptor keeps audit behaviour out of use cases and
/// guarantees it cannot be forgotten by a future feature module.
/// </remarks>
internal sealed class AuditableEntityInterceptor(IClock clock) : SaveChangesInterceptor
{
    private readonly IClock _clock = clock;

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTimeOffset now = _clock.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.ModifiedAtUtc = now;
                    break;
                default:
                    break;
            }
        }
    }
}
