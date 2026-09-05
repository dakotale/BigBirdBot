using DiscordBot.Services;

namespace DiscordBot.Helper;

/// <summary>
/// Keeps the keyword image store and the <c>ChatKeyword</c> table in sync:
/// removes rows whose local file has gone missing, and finds files sitting in a
/// keyword folder that no row points at. Run on a schedule (see the scheduler loop)
/// and on demand via the owner command.
/// </summary>
public sealed class KeywordMaintenanceService(KeywordService keywords, LoggingService logger)
{
    public readonly record struct Result(
        int DeadRowsRemoved, int OrphanFiles, long OrphanBytes, int OrphanFilesDeleted)
    {
        public override string ToString() =>
            $"{DeadRowsRemoved} dead row(s) removed; {OrphanFiles} orphan file(s) " +
            $"({OrphanBytes / 1024 / 1024} MB){(OrphanFilesDeleted > 0 ? $", {OrphanFilesDeleted} deleted" : "")}";
    }

    /// <summary>
    /// Deletes <c>ChatKeyword</c> rows whose resolved local file no longer exists. With
    /// <paramref name="purgeOrphanFiles"/>, also deletes files under a keyword folder that
    /// no row references; otherwise it only counts and logs them.
    /// </summary>
    public async Task<Result> ReconcileAsync(bool purgeOrphanFiles = false)
    {
        var entries = await keywords.GetLocalFileEntriesAsync();

        var deadRowIds = new List<int>();
        var liveFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (id, value) in entries)
        {
            string abs;
            try { abs = KeywordFiles.Resolve(value); }
            catch { deadRowIds.Add(id); continue; }

            if (File.Exists(abs)) liveFiles.Add(abs);
            else deadRowIds.Add(id);
        }

        int removed = await keywords.DeleteEntriesByIdAsync(deadRowIds);

        // Orphan files: physically present under a keyword folder, no row points at them.
        var orphans = new List<string>();
        long orphanBytes = 0;
        string root = Constants.Constants.keywordDirectory;

        if (Directory.Exists(root))
        {
            foreach (string dir in Directory.EnumerateDirectories(root))
            foreach (string file in Directory.EnumerateFiles(dir))
            {
                if (liveFiles.Contains(Path.GetFullPath(file))) continue;
                orphans.Add(file);
                try { orphanBytes += new FileInfo(file).Length; } catch { /* races */ }
            }
        }

        int deleted = 0;
        if (purgeOrphanFiles)
        {
            foreach (string f in orphans)
                try { File.Delete(f); deleted++; } catch { /* locked / gone */ }
        }

        var result = new Result(removed, orphans.Count, orphanBytes, deleted);
        await logger.InfoAsync($"[KeywordReconcile] {result}");
        return result;
    }
}
