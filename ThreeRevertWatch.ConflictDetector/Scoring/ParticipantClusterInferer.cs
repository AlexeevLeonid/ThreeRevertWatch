using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.ConflictDetector.State;

namespace ThreeRevertWatch.ConflictDetector.Scoring;

public sealed class ParticipantClusterInferer : IParticipantClusterInferer
{
    public IReadOnlyList<ParticipantClusterDto> Infer(ArticleRuntimeState state)
    {
        if (state.RevertEdges.Count < 2)
        {
            return [];
        }

        var clusters = new List<ParticipantClusterDto>();
        var mutual = state.RevertEdges
            .Select(edge => new
            {
                A = edge.FromUser,
                B = edge.ToUser,
                ReverseExists = state.RevertEdges.Any(other =>
                    string.Equals(other.FromUser, edge.ToUser, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(other.ToUser, edge.FromUser, StringComparison.OrdinalIgnoreCase))
            })
            .FirstOrDefault(edge => edge.ReverseExists);

        if (mutual is not null)
        {
            clusters.Add(new ParticipantClusterDto(
                "Cluster A",
                [mutual.A],
                0.72,
                [$"{mutual.A} and {mutual.B} reverted each other"]));
            clusters.Add(new ParticipantClusterDto(
                "Cluster B",
                [mutual.B],
                0.72,
                [$"{mutual.B} and {mutual.A} reverted each other"]));
        }

        var sameTarget = state.RevertEdges
            .GroupBy(edge => edge.ToUser, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Target = group.Key,
                Users = group.Select(edge => edge.FromUser).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            })
            .FirstOrDefault(group => group.Users.Count >= 2);

        if (sameTarget is not null)
        {
            clusters.Add(new ParticipantClusterDto(
                $"Cluster {(char)('A' + clusters.Count)}",
                sameTarget.Users,
                0.55,
                [$"Users repeatedly reverted the same participant: {sameTarget.Target}"]));
        }

        return clusters
            .GroupBy(cluster => string.Join('|', cluster.Users.Order(StringComparer.OrdinalIgnoreCase)), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Where(cluster => cluster.Confidence >= 0.5)
            .Take(3)
            .ToList();
    }
}

