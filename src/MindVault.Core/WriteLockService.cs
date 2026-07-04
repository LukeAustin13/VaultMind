using System.Text.Json;

namespace MindVault.Core;

/// <summary>
/// Cross-process write lock: a `.mindvault/write.lock` file held for the duration of one
/// mutation, so two MindVault instances (e.g. CLI on the desktop and the Pi container over a
/// synced folder) cannot interleave read-modify-write cycles on the same vault. Rules:
/// short-lived (one mutation), stale locks are detected by age and taken over, a fresh
/// foreign lock fails the write clearly with WRITE_LOCKED, and reads never touch it.
/// In-process reentrancy (e.g. a session start creating its log note) is handled with a
/// depth counter; callers must hold <see cref="VaultContext.Sync"/>, which makes it safe.
/// </summary>
public sealed class WriteLockService(VaultContext ctx)
{
    private sealed record LockInfo(int Pid, string Machine, DateTime CreatedUtc);

    private int _depth;

    public string LockFilePath => Path.Combine(ctx.MindVaultDir, "write.lock");

    /// <summary>Acquires the vault write lock (reentrant). Call while holding ctx.Sync.</summary>
    public IDisposable Acquire()
    {
        if (_depth == 0) EnterFileLock();
        _depth++;
        return new Releaser(this);
    }

    private void EnterFileLock()
    {
        Directory.CreateDirectory(ctx.MindVaultDir);
        if (TryCreateLockFile()) return;

        // Lock file exists. Take it over only if it is stale (crashed/hung holder).
        var staleAfter = TimeSpan.FromSeconds(Math.Max(ctx.Config.WriteLockStaleSeconds, 1));
        var info = ReadLockInfo();
        var age = DateTime.UtcNow - (info?.CreatedUtc ?? SafeLastWriteUtc());
        if (age > staleAfter)
        {
            try { File.Delete(LockFilePath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* holder may have just released */ }
            if (TryCreateLockFile()) return;
        }

        var holder = info is null ? "another process" : $"pid {info.Pid} on {info.Machine}";
        throw new WriteLockedException(
            $"The vault is locked for writing by {holder} (lock age {Math.Max((int)age.TotalSeconds, 0)}s, " +
            $"stale after {(int)staleAfter.TotalSeconds}s): {LockFilePath}. " +
            "Retry shortly. If no other MindVault instance is running, delete the lock file. " +
            "Read and search commands are not affected.");
    }

    private bool TryCreateLockFile()
    {
        try
        {
            using var stream = new FileStream(LockFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream);
            writer.Write(JsonSerializer.Serialize(
                new LockInfo(Environment.ProcessId, Environment.MachineName, DateTime.UtcNow), Json.Options));
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private LockInfo? ReadLockInfo()
    {
        try
        {
            return JsonSerializer.Deserialize<LockInfo>(File.ReadAllText(LockFilePath), Json.Options);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private DateTime SafeLastWriteUtc()
    {
        try { return File.GetLastWriteTimeUtc(LockFilePath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return DateTime.UtcNow; }
    }

    private void Release()
    {
        _depth--;
        if (_depth > 0) return;
        try { File.Delete(LockFilePath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover lock is recovered via stale detection, never a brick.
        }
    }

    private sealed class Releaser(WriteLockService owner) : IDisposable
    {
        private bool _released;

        public void Dispose()
        {
            if (_released) return;
            _released = true;
            owner.Release();
        }
    }
}
