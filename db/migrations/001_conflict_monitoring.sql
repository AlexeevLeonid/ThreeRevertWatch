CREATE TABLE IF NOT EXISTS conflict_topics (
    id text PRIMARY KEY,
    display_name text NOT NULL,
    wiki text NOT NULL,
    is_active boolean NOT NULL,
    config_json jsonb NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS topic_articles (
    topic_id text NOT NULL,
    wiki text NOT NULL,
    page_id bigint NOT NULL,
    title text NOT NULL,
    membership_status text NOT NULL,
    relevance_score double precision NOT NULL,
    reasons_json jsonb NOT NULL,
    first_seen_at timestamptz NOT NULL,
    last_seen_at timestamptz NOT NULL,
    PRIMARY KEY (topic_id, wiki, page_id)
);

CREATE TABLE IF NOT EXISTS classified_edits (
    id bigserial PRIMARY KEY,
    topic_id text NOT NULL,
    wiki text NOT NULL,
    page_id bigint NOT NULL,
    title text NOT NULL,
    revision_id bigint NOT NULL,
    parent_revision_id bigint NULL,
    user_name text NOT NULL,
    timestamp timestamptz NOT NULL,
    comment text NOT NULL,
    tags_json jsonb NOT NULL,
    action_type text NOT NULL,
    confidence double precision NOT NULL,
    reverted_users_json jsonb NOT NULL,
    reverted_revision_ids_json jsonb NOT NULL,
    fragment_ids_json jsonb NOT NULL,
    flags_json jsonb NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS revert_edges (
    id bigserial PRIMARY KEY,
    topic_id text NOT NULL,
    wiki text NOT NULL,
    page_id bigint NOT NULL,
    from_user text NOT NULL,
    to_user text NOT NULL,
    from_revision_id bigint NOT NULL,
    to_revision_id bigint NOT NULL,
    revert_type text NOT NULL,
    confidence double precision NOT NULL,
    fragment_id text NULL,
    timestamp timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS article_conflict_snapshots (
    topic_id text NOT NULL,
    wiki text NOT NULL,
    page_id bigint NOT NULL,
    title text NOT NULL,
    relevance_score double precision NOT NULL,
    conflict_score double precision NOT NULL,
    status text NOT NULL,
    recent_edit_count integer NOT NULL,
    recent_revert_count integer NOT NULL,
    recent_participant_count integer NOT NULL,
    snapshot_json jsonb NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (topic_id, wiki, page_id)
);

CREATE TABLE IF NOT EXISTS topic_snapshots (
    topic_id text PRIMARY KEY,
    conflict_score double precision NOT NULL,
    status text NOT NULL,
    active_article_count integer NOT NULL,
    recent_edit_count integer NOT NULL,
    recent_revert_count integer NOT NULL,
    recent_participant_count integer NOT NULL,
    snapshot_json jsonb NOT NULL,
    updated_at timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_topic_articles_topic_score ON topic_articles (topic_id, relevance_score DESC);
CREATE INDEX IF NOT EXISTS ix_classified_edits_article ON classified_edits (topic_id, wiki, page_id, timestamp DESC);
CREATE INDEX IF NOT EXISTS ix_revert_edges_article ON revert_edges (topic_id, wiki, page_id, timestamp DESC);
CREATE INDEX IF NOT EXISTS ix_article_conflict_snapshots_topic_score ON article_conflict_snapshots (topic_id, conflict_score DESC);

