CREATE TABLE repo_topic (
    repo_id bigint NOT NULL,
    topic_id bigint NOT NULL,
    PRIMARY KEY (repo_id, topic_id)
);
