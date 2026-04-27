using ThreeRevertWatch.Contracts;

namespace ThreeRevertWatch.Collector.Wikipedia;

public interface IRecentChangesClient
{
    Task<IReadOnlyList<RawEditEvent>> GetRecentChangesAsync(
        string wiki,
        int limit,
        IReadOnlyList<int> allowedNamespaces,
        CancellationToken cancellationToken);
}

