PRAGMA foreign_keys = ON;

CREATE TABLE user
(
    name TEXT PRIMARY KEY
);

CREATE TABLE alliance
(
    id   INTEGER PRIMARY KEY,
    name TEXT NOT NULL
);

CREATE TABLE user_alliance
(
    user_name   TEXT NOT NULL,
    alliance_id TEXT NOT NULL,
    FOREIGN KEY (user_name) REFERENCES user (name),
    FOREIGN KEY (alliance_id) REFERENCES alliance (id),
    PRIMARY KEY (user_name, alliance_id)
);

CREATE TABLE region
(
    id         INTEGER PRIMARY KEY,
    name       TEXT    NOT NULL,
    number     INTEGER NOT NULL,
    country_id INTEGER NOT NULL,
    tl_x       INTEGER NOT NULL,
    tl_y       INTEGER NOT NULL
);

CREATE TABLE pixel
(
    id        INTEGER PRIMARY KEY AUTOINCREMENT,
    px_x      INTEGER NOT NULL,
    px_y      INTEGER NOT NULL,
    region_id INTEGER NOT NULL,
    user_name TEXT    NOT NULL,
    date      TEXT    NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (region_id) REFERENCES region (id),
    FOREIGN KEY (user_name) REFERENCES user (name)
);