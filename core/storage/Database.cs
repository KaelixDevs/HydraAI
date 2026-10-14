using System.Runtime.InteropServices;

namespace Hydra.Core.Storage;

/// <summary>Prepared-statement adapter over the operating system's SQLite runtime.</summary>
public sealed class Database : IDisposable
{
    static Database() => NativeLibrary.SetDllImportResolver(typeof(Database).Assembly, (name, assembly, search) =>
        name == "hydra_sqlite" ? NativeLibrary.Load(OperatingSystem.IsWindows() ? "winsqlite3.dll" : "libsqlite3.so.0", assembly, search) : IntPtr.Zero);
    private IntPtr handle;
    private readonly object gate = new();
    public Database(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (Native.sqlite3_open(path, out handle) != 0) { Dispose(); throw new IOException("Cannot open the local database."); }
        Native.sqlite3_busy_timeout(handle, 5000);
        Execute("PRAGMA journal_mode=WAL");
        Execute("PRAGMA foreign_keys=ON");
        Migrate();
    }
    public void Execute(string sql, params string?[] values) => Query(sql, values);
    public List<Dictionary<string, string>> Query(string sql, params string?[] values)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, this);
            Check(Native.sqlite3_prepare_v2(handle, sql, -1, out var statement, IntPtr.Zero));
            try
            {
                for (var i = 0; i < values.Length; i++)
                    Check(values[i] is null ? Native.sqlite3_bind_null(statement, i + 1) :
                        Native.sqlite3_bind_text(statement, i + 1, values[i]!, -1, new IntPtr(-1)));
                var rows = new List<Dictionary<string, string>>();
                int result;
                while ((result = Native.sqlite3_step(statement)) == 100)
                {
                    var row = new Dictionary<string, string>();
                    for (var i = 0; i < Native.sqlite3_column_count(statement); i++)
                        row[Marshal.PtrToStringUTF8(Native.sqlite3_column_name(statement, i))!] =
                            Marshal.PtrToStringUTF8(Native.sqlite3_column_text(statement, i)) ?? "";
                    rows.Add(row);
                }
                if (result != 101) Check(result);
                return rows;
            }
            finally { Native.sqlite3_finalize(statement); }
        }
    }
    public void Transaction(Action action)
    {
        lock (gate)
        {
            Execute("BEGIN IMMEDIATE");
            try { action(); Execute("COMMIT"); }
            catch { Execute("ROLLBACK"); throw; }
        }
    }
    private void Check(int code)
    {
        if (code != 0) throw new IOException($"SQLite operation failed (code {code}).");
    }
    private void Migrate()
    {
        var version = int.Parse(Query("PRAGMA user_version")[0]["user_version"]);
        if (version > 1) throw new InvalidOperationException("Database is newer than this application.");
        if (version == 1) return;
        Transaction(() =>
        {
            foreach (var sql in Schema) Execute(sql);
            Execute("PRAGMA user_version=1");
        });
    }
    private static readonly string[] Schema =
    [
        "CREATE TABLE Users(id TEXT PRIMARY KEY, username TEXT NOT NULL, email TEXT, password_hash TEXT)",
        "CREATE TABLE Projects(id TEXT PRIMARY KEY, name TEXT NOT NULL, root TEXT NOT NULL, instructions TEXT NOT NULL DEFAULT '')",
        "CREATE TABLE Chats(id TEXT PRIMARY KEY, title TEXT NOT NULL, profile TEXT NOT NULL, created TEXT NOT NULL, pinned INTEGER NOT NULL DEFAULT 0, project_id TEXT REFERENCES Projects(id) ON DELETE SET NULL)",
        "CREATE TABLE Messages(id TEXT PRIMARY KEY, chat_id TEXT NOT NULL REFERENCES Chats(id) ON DELETE CASCADE, role TEXT NOT NULL CHECK(role IN ('user','assistant','system')), content TEXT NOT NULL, created TEXT NOT NULL, backend TEXT NOT NULL DEFAULT '', state TEXT NOT NULL DEFAULT 'complete')",
        "CREATE INDEX messages_chat ON Messages(chat_id, created)",
        "CREATE INDEX chats_pinned_created ON Chats(pinned DESC, created DESC)",
        "CREATE TABLE Settings(key TEXT PRIMARY KEY, value TEXT NOT NULL)",
        "CREATE TABLE Models(id TEXT PRIMARY KEY, manifest TEXT NOT NULL)",
        "CREATE TABLE InstalledModels(id TEXT PRIMARY KEY REFERENCES Models(id) ON DELETE CASCADE, path TEXT NOT NULL, sha256 TEXT NOT NULL, installed TEXT NOT NULL)",
        "CREATE TABLE Files(id TEXT PRIMARY KEY, chat_id TEXT REFERENCES Chats(id) ON DELETE CASCADE, name TEXT NOT NULL, content TEXT NOT NULL, sha256 TEXT NOT NULL)",
        "CREATE TABLE Memories(id TEXT PRIMARY KEY, content TEXT NOT NULL, created TEXT NOT NULL)",
        "CREATE TABLE ResearchSessions(id TEXT PRIMARY KEY, query TEXT NOT NULL, created TEXT NOT NULL)",
        "CREATE TABLE Sources(id TEXT PRIMARY KEY, session_id TEXT REFERENCES ResearchSessions(id) ON DELETE CASCADE, url TEXT NOT NULL, title TEXT NOT NULL, retrieved TEXT NOT NULL, sha256 TEXT NOT NULL)",
        "CREATE TABLE ModelDownloads(id TEXT PRIMARY KEY, url TEXT NOT NULL, expected_hash TEXT NOT NULL, bytes INTEGER NOT NULL DEFAULT 0, state TEXT NOT NULL)",
        "CREATE TABLE ToolPermissions(id TEXT PRIMARY KEY, root TEXT NOT NULL, capability TEXT NOT NULL, expires TEXT)"
    ];
    public void Dispose()
    {
        lock (gate) { if (handle != IntPtr.Zero) { Native.sqlite3_close_v2(handle); handle = IntPtr.Zero; } }
    }
    private static class Native
    {
        private const string Lib = "hydra_sqlite";
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr db);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_close_v2(IntPtr db);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_busy_timeout(IntPtr db, int milliseconds);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_prepare_v2(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int length, out IntPtr statement, IntPtr tail);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_bind_text(IntPtr statement, int index, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, int length, IntPtr destructor);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_bind_null(IntPtr statement, int index);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_step(IntPtr statement);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_finalize(IntPtr statement);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_column_count(IntPtr statement);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_column_name(IntPtr statement, int index);
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_column_text(IntPtr statement, int index);
    }
}
