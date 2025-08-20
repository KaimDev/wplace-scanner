PRAGMA foreign_keys = ON;

CREATE TABLE user
(
    id  INTEGER PRIMARY KEY,
    name TEXT NOT NULL,
    date TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE alliance
(
    id   INTEGER PRIMARY KEY,
    name TEXT NOT NULL,
    date TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE user_alliance
(
    user_id   TEXT NOT NULL,
    alliance_id TEXT NOT NULL,
    FOREIGN KEY (user_id) REFERENCES user (id),
    FOREIGN KEY (alliance_id) REFERENCES alliance (id),
    PRIMARY KEY (user_id, alliance_id)
);

CREATE TABLE region
(
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    region_id  INTEGER NOT NULL,
    name       TEXT    NOT NULL,
    number     INTEGER NOT NULL,
    country_id INTEGER NOT NULL,
    tl_x       INTEGER NOT NULL,
    tl_y       INTEGER NOT NULL,
    date TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE pixel
(
    id        INTEGER PRIMARY KEY AUTOINCREMENT,
    px_x      INTEGER NOT NULL,
    px_y      INTEGER NOT NULL,
    region_id INTEGER NOT NULL,
    user_id TEXT    NOT NULL,
    date      TEXT    NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (region_id) REFERENCES region (id),
    FOREIGN KEY (user_id) REFERENCES user (id)
);
