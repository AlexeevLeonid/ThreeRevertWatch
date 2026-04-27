using System.Text.RegularExpressions;
using ThreeRevertWatch.Contracts;
using ThreeRevertWatch.ConflictDetector.State;

namespace ThreeRevertWatch.ConflictDetector.Classification;

public sealed partial class ExplainableEditClassifier : IEditClassifier
{
    public Task<ClassifiedEditDto> ClassifyAsync(
        TopicMatchedEditEvent matchedEdit,
        RevisionDetails revision,
        ArticleRuntimeState state,
        CancellationToken cancellationToken)
    {
        var raw = matchedEdit.RawEdit;
        var tags = revision.Tags.Count > 0 ? revision.Tags : raw.Tags;
        var comment = string.IsNullOrWhiteSpace(revision.Comment) ? raw.Comment : revision.Comment;
        var flags = new List<string>();
        var revertedUsers = new List<string>();
        var revertedRevisionIds = new List<long>();
        var action = EditActionType.OrdinaryEdit;
        var confidence = 0.55;

        if (raw.IsBot || tags.Any(IsBotTag) || BotUserNameRegex().IsMatch(revision.User))
        {
            action = EditActionType.BotEdit;
            confidence = 0.95;
            flags.Add("bot signal");
        }
        else if (!string.IsNullOrWhiteSpace(revision.Sha1))
        {
            var revertedTo = state.RecentRevisions.LastOrDefault(r =>
                !string.IsNullOrWhiteSpace(r.Sha1)
                && string.Equals(r.Sha1, revision.Sha1, StringComparison.OrdinalIgnoreCase));

            if (revertedTo is not null)
            {
                var revertedCandidates = state.FindRevisionsBetween(revertedTo.RevisionId, revision.RevisionId);
                var nonCurrentUsers = revertedCandidates
                    .Where(r => !string.Equals(r.User, revision.User, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                revertedRevisionIds.AddRange(revertedCandidates.Select(r => r.RevisionId));
                revertedUsers.AddRange(nonCurrentUsers.Select(r => r.User).Distinct(StringComparer.OrdinalIgnoreCase));

                if (revertedCandidates.Count > 0 && nonCurrentUsers.Count == 0)
                {
                    action = EditActionType.SelfRevert;
                    confidence = 0.92;
                    flags.Add($"restored own previous state r{revertedTo.RevisionId}");
                }
                else if (revertedCandidates.Count > 0)
                {
                    action = CleanupAction(comment, tags) ?? EditActionType.ExactRevert;
                    confidence = action == EditActionType.ExactRevert ? 0.96 : 0.88;
                    flags.Add($"sha1 restored revision {revertedTo.RevisionId}");
                    flags.Add($"reverted revisions: {string.Join(',', revertedRevisionIds)}");
                    if (action != EditActionType.ExactRevert)
                    {
                        flags.Add("cleanup revert signal");
                    }
                }
            }
        }

        if (action == EditActionType.OrdinaryEdit && HasExplicitRevertSignal(comment, tags))
        {
            action = CleanupAction(comment, tags) ?? EditActionType.ExactRevert;
            confidence = action == EditActionType.ExactRevert ? 0.68 : 0.72;
            flags.Add("explicit revert comment or tag");
            if (revertedRevisionIds.Count == 0)
            {
                flags.Add("reverted revisions unknown");
            }
        }

        if (action == EditActionType.OrdinaryEdit && IsMaintenance(comment, tags))
        {
            action = EditActionType.MaintenanceEdit;
            confidence = 0.70;
            flags.Add("maintenance edit signal");
        }

        if (action == EditActionType.OrdinaryEdit && IsTalkPageCoordination(raw.Title, comment))
        {
            action = EditActionType.TalkPageCoordination;
            confidence = 0.72;
            flags.Add("talk page coordination");
        }

        return Task.FromResult(new ClassifiedEditDto(
            revision.Wiki,
            matchedEdit.TopicId,
            revision.PageId,
            revision.Title,
            revision.RevisionId,
            revision.ParentRevisionId,
            revision.User,
            revision.Timestamp,
            comment,
            tags,
            action,
            confidence,
            revertedUsers,
            revertedRevisionIds,
            [],
            flags));
    }

    private static bool IsBotTag(string tag)
        => tag.Contains("bot", StringComparison.OrdinalIgnoreCase);

    private static bool HasExplicitRevertSignal(string comment, IReadOnlyList<string> tags)
        => RevertRegex().IsMatch(comment)
           || tags.Any(tag => RevertRegex().IsMatch(tag));

    private static EditActionType? CleanupAction(string comment, IReadOnlyList<string> tags)
    {
        var text = $"{comment} {string.Join(' ', tags)}";
        if (VandalismRegex().IsMatch(text))
        {
            return EditActionType.VandalismCleanup;
        }

        if (SpamRegex().IsMatch(text))
        {
            return EditActionType.SpamCleanup;
        }

        if (CopyvioRegex().IsMatch(text))
        {
            return EditActionType.CopyvioCleanup;
        }

        return null;
    }

    private static bool IsMaintenance(string comment, IReadOnlyList<string> tags)
    {
        var text = $"{comment} {string.Join(' ', tags)}";
        return MaintenanceRegex().IsMatch(text);
    }

    private static bool IsTalkPageCoordination(string title, string comment)
        => title.StartsWith("Обсуждение:", StringComparison.OrdinalIgnoreCase)
           || title.StartsWith("Talk:", StringComparison.OrdinalIgnoreCase)
           || comment.Contains("consensus", StringComparison.OrdinalIgnoreCase)
           || comment.Contains("консенсус", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"bot|бот", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BotUserNameRegex();

    [GeneratedRegex(@"отмена|откат|rvv|\brv\b|revert|undo|rollback", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RevertRegex();

    [GeneratedRegex(@"вандализм|rvv|vandal", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VandalismRegex();

    [GeneratedRegex(@"spam|спам", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SpamRegex();

    [GeneratedRegex(@"copyvio|нарушение авторских прав", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CopyvioRegex();

    [GeneratedRegex(@"категори|шаблон|оформлен|typo|wikify|орфограф|format|maintenance", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MaintenanceRegex();
}
