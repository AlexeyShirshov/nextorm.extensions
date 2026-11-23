CREATE TABLE repo_topic (
    repo_id INTEGER NOT NULL,
    topic_id INTEGER NOT NULL,
    PRIMARY KEY (repo_id, topic_id)
);
