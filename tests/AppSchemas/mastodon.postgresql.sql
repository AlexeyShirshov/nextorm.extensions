CREATE TABLE tags (
    id bigserial PRIMARY KEY,
    name varchar NOT NULL DEFAULT '',
    created_at timestamp NOT NULL,
    updated_at timestamp NOT NULL,
    usable boolean,
    trendable boolean,
    listable boolean,
    reviewed_at timestamp,
    requested_review_at timestamp,
    last_status_at timestamp,
    max_score double precision,
    max_score_at timestamp,
    display_name varchar
);

CREATE UNIQUE INDEX index_tags_on_name_lower_btree
    ON tags (lower(name::text) text_pattern_ops);
