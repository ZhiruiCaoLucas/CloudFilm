using CloudFilm.Application.Abstractions;
using CloudFilm.Application.Auditing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using NSubstitute.ClearExtensions;

namespace CloudFilm.Api.Tests.ReadState;

/// <summary>
/// A unit of work that runs the operation and remembers what happened.
/// </summary>
/// <remarks>
/// <para>
/// A hand-written fake rather than a substitute, because the behaviour under
/// test is what the endpoint puts <em>inside</em> the boundary. It records the
/// order of the calls the operation made and whether the boundary closed by
/// committing or by unwinding, which is what turns "the endpoint used a
/// transaction" from an assumption into an assertion.
/// </para>
/// <para>
/// It is not a transaction. It opens no connection and rolls nothing back - the
/// real one does that, against PostgreSQL, and no test in this suite has a
/// database. What this proves is that the mutation and the audit write are
/// composed into a single operation and that a failure in either one prevents
/// the commit. That the commit is atomic in the database is a property of
/// <c>TransactionalUnitOfWork</c> and is verified against a live stack.
/// </para>
/// </remarks>
public sealed class RecordingUnitOfWork : IUnitOfWork
{
    private readonly List<string> _events = [];

    /// <summary>Gets what happened, in order.</summary>
    public IReadOnlyList<string> Events => _events;

    /// <summary>Gets the number of operations that committed.</summary>
    public int Commits { get; private set; }

    /// <summary>Gets the number of operations that unwound.</summary>
    public int Rollbacks { get; private set; }

    /// <summary>Notes that something happened inside an operation.</summary>
    /// <param name="name">What happened.</param>
    public void Record(string name) => _events.Add(name);

    /// <summary>Forgets everything, so each test starts from silence.</summary>
    public void Reset()
    {
        _events.Clear();
        Commits = 0;
        Rollbacks = 0;
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        _events.Add("begin");

        bool committed = false;

        try
        {
            TResult result = await operation(cancellationToken);

            _events.Add("commit");
            Commits++;
            committed = true;

            return result;
        }
        finally
        {
            // A finally rather than a catch: the real boundary rolls back by
            // disposing an uncommitted transaction, not by handling the
            // exception, and this records the same thing without swallowing
            // anything or pretending to know what went wrong.
            if (!committed)
            {
                _events.Add("rollback");
                Rollbacks++;
            }
        }
    }
}

/// <summary>
/// A host with the read-state store, the audit log and the unit of work
/// substituted.
/// </summary>
/// <remarks>
/// The real store and unit of work need PostgreSQL, which this suite runs
/// without. Faking them is also what makes the assertions possible: what matters
/// is the account the endpoint acts for, whether it recorded an audit event, and
/// whether it did both inside one boundary.
/// </remarks>
public sealed class ReadStateApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Gets the substituted read-state store.</summary>
    public IStudyReadStateStore ReadState { get; } = Substitute.For<IStudyReadStateStore>();

    /// <summary>Gets the substituted audit log.</summary>
    public IAuditLogService AuditLog { get; } = Substitute.For<IAuditLogService>();

    /// <summary>Gets the recording unit of work.</summary>
    public RecordingUnitOfWork UnitOfWork { get; } = new();

    /// <summary>
    /// Returns every double to its as-created state.
    /// </summary>
    /// <remarks>
    /// <c>ClearOptions.All</c>, not <c>ClearReceivedCalls()</c>: the latter keeps
    /// configured return values, so a test that arranges a failure leaves every
    /// later test failing in a way that depends on the order xUnit picked. The
    /// defaults are then re-armed explicitly rather than left to NSubstitute's
    /// automatic values, so what each test starts from is stated.
    /// </remarks>
    public void ResetSubstitutes()
    {
        ReadState.ClearSubstitute(ClearOptions.All);
        AuditLog.ClearSubstitute(ClearOptions.All);
        UnitOfWork.Reset();

        // Nothing has been read by anybody.
        ReadState
            .IsReadAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Both mutations report a real transition by default; tests that need the
        // idempotent case say so.
        ReadState
            .MarkReadAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                UnitOfWork.Record("mark-read");
                return true;
            });

        ReadState
            .MarkUnreadAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                UnitOfWork.Record("mark-unread");
                return true;
            });

        AuditLog
            .RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                UnitOfWork.Record("audit");
                return Task.CompletedTask;
            });
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development, like the other factories: under Production the host loads
        // appsettings.json alone, where Jwt:SigningKey is empty, and
        // ValidateOnStart would refuse to start for reasons unrelated to the test.
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            foreach (ServiceDescriptor descriptor in services
                .Where(service => service.ServiceType == typeof(IHostedService)
                    && service.ImplementationType?.Namespace?.StartsWith("CloudFilm", StringComparison.Ordinal) == true)
                .ToList())
            {
                services.Remove(descriptor);
            }

            services.RemoveAll<IStudyReadStateStore>();
            services.AddScoped(_ => ReadState);

            services.RemoveAll<IAuditLogService>();
            services.AddScoped(_ => AuditLog);

            services.RemoveAll<IUnitOfWork>();
            services.AddScoped<IUnitOfWork>(_ => UnitOfWork);
        });
    }
}
