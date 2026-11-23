CREATE TABLE wp_termmeta (
    meta_id bigint(20) unsigned NOT NULL AUTO_INCREMENT,
    term_id bigint(20) unsigned NOT NULL DEFAULT 0,
    meta_key varchar(255) DEFAULT NULL,
    meta_value longtext,
    PRIMARY KEY (meta_id),
    KEY term_id (term_id),
    KEY meta_key (meta_key(191))
) DEFAULT CHARACTER SET utf8mb4;

CREATE TABLE wp_terms (
    term_id bigint(20) unsigned NOT NULL AUTO_INCREMENT,
    name varchar(200) NOT NULL DEFAULT '',
    slug varchar(200) NOT NULL DEFAULT '',
    term_group bigint(10) NOT NULL DEFAULT 0,
    PRIMARY KEY (term_id),
    KEY slug (slug(191)),
    KEY name (name(191))
) DEFAULT CHARACTER SET utf8mb4;
