namespace CompanionCore.Memory;

internal static class MemoryStoreSchema
{
    internal const string CreateVersionOne = """
        PRAGMA user_version = 1;

        CREATE TABLE schema_info (
            singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
            schema_version INTEGER NOT NULL
        );

        INSERT INTO schema_info (singleton, schema_version) VALUES (1, 1);

        CREATE TABLE append_operations (
            operation_id TEXT PRIMARY KEY NOT NULL,
            operation_checksum TEXT NOT NULL,
            canonical_payload BLOB NOT NULL,
            committed_at_utc TEXT NOT NULL,
            journal_sequence INTEGER NOT NULL UNIQUE CHECK (journal_sequence > 0)
        );

        CREATE TABLE memory_records (
            record_id TEXT PRIMARY KEY NOT NULL,
            operation_id TEXT NOT NULL,
            schema_version INTEGER NOT NULL CHECK (schema_version = 1),
            created_at_utc TEXT NOT NULL,
            scope INTEGER NOT NULL,
            source_kind INTEGER NOT NULL,
            confidence REAL NOT NULL CHECK (confidence >= 0.0 AND confidence <= 1.0),
            subject_key TEXT NOT NULL,
            entity_references_json TEXT NOT NULL,
            application_reference TEXT NULL,
            game_reference TEXT NULL,
            save_reference TEXT NULL,
            session_reference TEXT NULL,
            visible_recollection TEXT NOT NULL,
            retrieval_metadata_json TEXT NOT NULL,
            record_checksum TEXT NOT NULL,
            committed INTEGER NOT NULL DEFAULT 1 CHECK (committed = 1),
            FOREIGN KEY (operation_id) REFERENCES append_operations(operation_id)
        );

        CREATE TABLE memory_links (
            source_record_id TEXT NOT NULL,
            target_record_id TEXT NOT NULL,
            link_kind INTEGER NOT NULL,
            PRIMARY KEY (source_record_id, target_record_id, link_kind),
            FOREIGN KEY (source_record_id) REFERENCES memory_records(record_id),
            FOREIGN KEY (target_record_id) REFERENCES memory_records(record_id)
        );

        CREATE INDEX memory_records_subject_idx
            ON memory_records(subject_key);
        CREATE INDEX memory_links_target_idx
            ON memory_links(target_record_id, link_kind);

        CREATE TRIGGER immutable_schema_info_update
        BEFORE UPDATE ON schema_info
        BEGIN SELECT RAISE(ABORT, 'append-only schema metadata'); END;

        CREATE TRIGGER immutable_schema_info_delete
        BEFORE DELETE ON schema_info
        BEGIN SELECT RAISE(ABORT, 'append-only schema metadata'); END;

        CREATE TRIGGER immutable_append_operations_update
        BEFORE UPDATE ON append_operations
        BEGIN SELECT RAISE(ABORT, 'append-only committed operation'); END;

        CREATE TRIGGER immutable_append_operations_delete
        BEFORE DELETE ON append_operations
        BEGIN SELECT RAISE(ABORT, 'append-only committed operation'); END;

        CREATE TRIGGER immutable_memory_records_update
        BEFORE UPDATE ON memory_records
        BEGIN SELECT RAISE(ABORT, 'append-only committed memory'); END;

        CREATE TRIGGER immutable_memory_records_delete
        BEFORE DELETE ON memory_records
        BEGIN SELECT RAISE(ABORT, 'append-only committed memory'); END;

        CREATE TRIGGER immutable_memory_links_update
        BEFORE UPDATE ON memory_links
        BEGIN SELECT RAISE(ABORT, 'append-only committed link'); END;

        CREATE TRIGGER immutable_memory_links_delete
        BEFORE DELETE ON memory_links
        BEGIN SELECT RAISE(ABORT, 'append-only committed link'); END;
        """;

    internal static readonly string[] RequiredObjectNames =
    [
        "schema_info",
        "append_operations",
        "memory_records",
        "memory_links",
        "immutable_schema_info_update",
        "immutable_schema_info_delete",
        "immutable_append_operations_update",
        "immutable_append_operations_delete",
        "immutable_memory_records_update",
        "immutable_memory_records_delete",
        "immutable_memory_links_update",
        "immutable_memory_links_delete",
    ];

    internal static readonly IReadOnlyDictionary<string, string> ExactUserObjects =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["schema_info"] = "table",
            ["append_operations"] = "table",
            ["memory_records"] = "table",
            ["memory_links"] = "table",
            ["memory_records_subject_idx"] = "index",
            ["memory_links_target_idx"] = "index",
            ["immutable_schema_info_update"] = "trigger",
            ["immutable_schema_info_delete"] = "trigger",
            ["immutable_append_operations_update"] = "trigger",
            ["immutable_append_operations_delete"] = "trigger",
            ["immutable_memory_records_update"] = "trigger",
            ["immutable_memory_records_delete"] = "trigger",
            ["immutable_memory_links_update"] = "trigger",
            ["immutable_memory_links_delete"] = "trigger",
        };

    internal static readonly IReadOnlyDictionary<string, string[]> RequiredColumns =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["schema_info"] = ["singleton", "schema_version"],
            ["append_operations"] =
            [
                "operation_id",
                "operation_checksum",
                "canonical_payload",
                "committed_at_utc",
                "journal_sequence",
            ],
            ["memory_records"] =
            [
                "record_id",
                "operation_id",
                "schema_version",
                "created_at_utc",
                "scope",
                "source_kind",
                "confidence",
                "subject_key",
                "entity_references_json",
                "application_reference",
                "game_reference",
                "save_reference",
                "session_reference",
                "visible_recollection",
                "retrieval_metadata_json",
                "record_checksum",
                "committed",
            ],
            ["memory_links"] = ["source_record_id", "target_record_id", "link_kind"],
        };
}
