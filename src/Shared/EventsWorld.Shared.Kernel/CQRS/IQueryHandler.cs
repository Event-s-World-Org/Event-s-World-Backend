using EventsWorld.Shared.Kernel.Results;

namespace EventsWorld.Shared.Kernel.CQRS;

public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
