namespace Hydra.Core.Storage;

public record Chat(string Id, string Title, string Profile, bool Pinned);
public record Message(string Id, string Role, string Content, string Backend, string State);

public sealed class ChatStore(Database db)
{
    public string Create(string title, string profile)
    {
        var id = Guid.NewGuid().ToString("N");
        db.Execute("INSERT INTO Chats(id,title,profile,created) VALUES(?,?,?,?)", id, title, profile, DateTimeOffset.UtcNow.ToString("O"));
        return id;
    }
    public IReadOnlyList<Chat> List(string search = "") => db.Query(
        "SELECT * FROM Chats WHERE instr(lower(title),lower(?))>0 ORDER BY pinned DESC,created DESC", search)
        .Select(r => new Chat(r["id"], r["title"], r["profile"], r["pinned"] == "1")).ToList();
    public IReadOnlyList<Message> Messages(string chat) => db.Query("SELECT * FROM Messages WHERE chat_id=? ORDER BY created,rowid", chat)
        .Select(r => new Message(r["id"], r["role"], r["content"], r["backend"], r["state"])).ToList();
    public string Add(string chat, string role, string content, string backend = "", string state = "complete")
    {
        var id = Guid.NewGuid().ToString("N");
        db.Execute("INSERT INTO Messages(id,chat_id,role,content,created,backend,state) VALUES(?,?,?,?,?,?,?)", id, chat, role, content, DateTimeOffset.UtcNow.ToString("O"), backend, state);
        return id;
    }
    public void Rename(string chat, string title) => db.Execute("UPDATE Chats SET title=? WHERE id=?", title, chat);
    public void Pin(string chat, bool pin) => db.Execute("UPDATE Chats SET pinned=? WHERE id=?", pin ? "1" : "0", chat);
    public void Delete(string chat) => db.Execute("DELETE FROM Chats WHERE id=?", chat);
    public string Branch(string chat)
    {
        var original = List().Single(c => c.Id == chat);
        var copy = "";
        db.Transaction(() =>
        {
            copy = Create(original.Title + " · branch", original.Profile);
            foreach (var m in Messages(chat)) Add(copy, m.Role, m.Content, m.Backend, m.State);
            foreach (var file in db.Query("SELECT * FROM Files WHERE chat_id=?", chat))
                db.Execute("INSERT INTO Files VALUES(?,?,?,?,?)", Guid.NewGuid().ToString("N"), copy, file["name"], file["content"], file["sha256"]);
        });
        return copy;
    }
    public string GetSetting(string key, string fallback = "") => db.Query("SELECT value FROM Settings WHERE key=?", key).FirstOrDefault()?.GetValueOrDefault("value") ?? fallback;
    public void SetSetting(string key, string value) => db.Execute("INSERT INTO Settings VALUES(?,?) ON CONFLICT(key) DO UPDATE SET value=excluded.value", key, value);
}
