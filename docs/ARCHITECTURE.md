# Three Revert Watch Architecture

Three Revert Watch is a realtime conflict topic monitoring and edit-war tracking system for Wikipedia.

The product model is:

```text
ConflictTopic
  -> TopicArticle
    -> ArticleConflictState
      -> ClassifiedEdit
      -> RevertEdges
      -> Participants
      -> DisputedFragments
      -> ConflictScore
```

## Services

- `ThreeRevertWatch.Collector` polls Wikipedia RecentChanges and publishes raw edits to Kafka.
- `ThreeRevertWatch.TopicMatcher` consumes raw edits, applies manual/rule-based topic membership, uses cached Wikipedia page categories when title rules are insufficient, updates `topic_articles`, and publishes matched topic edits.
- `ThreeRevertWatch.ConflictDetector` consumes matched edits, fetches minimal revision details, classifies edit actions, updates article runtime state, persists evidence, and publishes article conflict updates.
- `ThreeRevertWatch.Aggregator` consumes article conflict updates, builds article/topic read models in Postgres and Redis, and exposes read APIs.
- `ThreeRevertWatch.Gateway` proxies REST APIs and broadcasts realtime conflict updates through SignalR.
- `ThreeRevertWatch.Frontend` is a Blazor Server operational dashboard for conflict topics, topic articles, article evidence, reverts, participants, and neutral behavioral clusters.

Removed from the old architecture:

- trend/anomaly/baseline analytics
- ClickHouse trend model
- SPARQL/Wikidata classifier as mandatory path
- baseline scheduler
- trend UI and trend SignalR hub

## Kafka Topics

- `wiki.raw-edits` keyed by `PageId`
- `wiki.topic-matched-edits` keyed by `PageId`
- `wiki.article-conflict-updates` keyed by `{TopicId}:{PageId}`
- `wiki.topic-conflict-updates` keyed by `TopicId`

Kafka partition keys keep article edits ordered inside a page. The workers process messages sequentially per consumer loop; per-page order depends on all events for that page using the same key.

## Topic Matching

Topic membership is intentionally rule/manual first:

- exact seed page ids
- exact seed titles
- include/exclude title keywords
- cached `topic_articles` memberships
- Wikipedia page categories fetched only when configured category rules can improve or reject a weak title match

The page category lookup uses `prop=categories` with hidden categories excluded and is cached in the TopicMatcher process. Edit tags from RecentChanges are still used for edit-action classification; they are not treated as article topics.

## Persistence

Postgres stores canonical state/read models:

- `conflict_topics`
- `topic_articles`
- `classified_edits`
- `revert_edges`
- `article_conflict_snapshots`
- `topic_snapshots`

Redis stores hot snapshots for low-latency reads in Aggregator.

## Conflict Detection

Edit classification is explainable and local:

- bot edits from raw flags, tags, and username patterns
- exact reverts from revision SHA1 returning to a recent previous state
- self-reverts when reverted intermediate revisions belong to the same user
- explicit reverts from comments/tags such as `отмена`, `откат`, `rv`, `revert`, `undo`, `rollback`, `rvv`
- cleanup reverts for vandalism, spam, and copyvio signals
- maintenance edits for formatting/category/template/typo signals
- ordinary edits as fallback

Conflict scoring is article-state based, not single-edit ML:

- recent edits
- non-self reverts
- reciprocal revert pairs
- participant count
- disputed fragments when available
- three-revert risk
- cleanup revert penalty

Participant clusters are neutral behavioral clusters only: `Cluster A`, `Cluster B`, `Cluster C`. The system does not infer political sides.

## TODO

- Partial revert detection is represented in DTOs but not deeply implemented.
- Disputed fragment extraction is a stub unless `DeepDiffEnabled` is later implemented.
- Optional periodic topic expansion can be added, but Wikidata/SPARQL must remain non-blocking and non-primary.
