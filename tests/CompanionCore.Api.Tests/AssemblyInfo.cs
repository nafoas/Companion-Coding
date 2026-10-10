// Bridge tests are dominated by durable disk syncs (SQLite FULL and journal flushes).
// Running them one class at a time bounds this suite to a single sync stream, so it
// cannot starve concurrently running process-level suites on a shared CI runner.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
