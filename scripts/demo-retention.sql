\echo Demo retention cleanup started.

WITH deleted AS (
    DELETE FROM classified_edits
    WHERE "timestamp" < now() - (:'edit_retention_days' || ' days')::interval
    RETURNING 1
)
SELECT 'classified_edits' AS table_name, count(*) AS deleted_rows FROM deleted;

WITH deleted AS (
    DELETE FROM revert_edges
    WHERE "timestamp" < now() - (:'edit_retention_days' || ' days')::interval
    RETURNING 1
)
SELECT 'revert_edges' AS table_name, count(*) AS deleted_rows FROM deleted;

WITH deleted AS (
    DELETE FROM article_conflict_snapshots
    WHERE updated_at < now() - (:'snapshot_retention_days' || ' days')::interval
    RETURNING 1
)
SELECT 'article_conflict_snapshots' AS table_name, count(*) AS deleted_rows FROM deleted;

WITH deleted AS (
    DELETE FROM topic_snapshots
    WHERE updated_at < now() - (:'snapshot_retention_days' || ' days')::interval
    RETURNING 1
)
SELECT 'topic_snapshots' AS table_name, count(*) AS deleted_rows FROM deleted;

WITH deleted AS (
    DELETE FROM topic_articles
    WHERE membership_status = 'Candidate'
      AND last_seen_at < now() - (:'candidate_retention_days' || ' days')::interval
    RETURNING 1
)
SELECT 'topic_articles_candidates' AS table_name, count(*) AS deleted_rows FROM deleted;

\echo Demo retention cleanup finished.
