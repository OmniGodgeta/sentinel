using Godot;

namespace Sentinel.Meta;

/// <summary>
/// Optional cloud save via Supabase (project "beyond-game") — email + password
/// account, one row per user in the `saves` table (RLS-scoped to auth.uid()).
/// Fully optional: guests just keep playing off the local user://save.json like
/// before. Built 2026-09-15 because the user lost their save reinstalling the
/// APK to update — see docs/ROADMAP.md for the fuller writeup.
///
/// Session (access/refresh token + user id/email) persists in user://account.json
/// — separate from save.json on purpose, so wiping one never touches the other.
/// Every network call is fire-and-forget from the caller's point of view and
/// fails silent (no popups, no blocking) — this must never be able to strand a
/// player who's offline or whose token expired.
/// </summary>
public sealed partial class CloudSave : Node
{
    // Supabase project "beyond-game" (org ShadowSwords). The anon key is meant to
    // be public — every request is still scoped server-side by RLS to auth.uid().
    private const string Url = "https://qquzgugvozelkurrpzys.supabase.co";
    private const string AnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InFxdXpndWd2b3plbGt1cnJwenlzIiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODk1MTMzOTMsImV4cCI6MjEwNTA4OTM5M30.E7JIsX6tVHSSVn4VZqrSCACwiN0t7zKqT3UfES7aF1s";
    private const string AccountPath = "user://account.json";

    public static CloudSave Instance { get; private set; } = null!;

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public string? UserId { get; private set; }
    public string? UserEmail { get; private set; }
    public bool LoggedIn => !string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(UserId);

    public override void _Ready()
    {
        Instance = this;
        LoadAccount();
    }

    private void LoadAccount()
    {
        if (!FileAccess.FileExists(AccountPath)) return;
        try
        {
            using var f = FileAccess.Open(AccountPath, FileAccess.ModeFlags.Read);
            var d = Json.ParseString(f.GetAsText()).AsGodotDictionary();
            AccessToken = d.TryGetValue("access_token", out var a) ? a.AsString() : null;
            RefreshToken = d.TryGetValue("refresh_token", out var r) ? r.AsString() : null;
            UserId = d.TryGetValue("user_id", out var u) ? u.AsString() : null;
            UserEmail = d.TryGetValue("email", out var e) ? e.AsString() : null;
        }
        catch { /* corrupt/missing — just stay logged out */ }
    }

    private void SaveAccount()
    {
        try
        {
            using var f = FileAccess.Open(AccountPath, FileAccess.ModeFlags.Write);
            var d = new Godot.Collections.Dictionary
            {
                ["access_token"] = AccessToken ?? "", ["refresh_token"] = RefreshToken ?? "",
                ["user_id"] = UserId ?? "", ["email"] = UserEmail ?? "",
            };
            f.StoreString(Json.Stringify(d));
        }
        catch { }
    }

    public void Logout()
    {
        AccessToken = RefreshToken = UserId = UserEmail = null;
        if (FileAccess.FileExists(AccountPath)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(AccountPath));
    }

    private HttpRequest NewReq()
    {
        var r = new HttpRequest { UseThreads = true, Timeout = 10 };
        AddChild(r);
        return r;
    }

    private static string[] AuthHeaders(string? bearer = null) => bearer == null
        ? new[] { "apikey: " + AnonKey, "Content-Type: application/json" }
        : new[] { "apikey: " + AnonKey, "Authorization: Bearer " + bearer, "Content-Type: application/json" };

    /// <summary>Create an account. On success (email confirmation disabled) logs
    /// straight in; otherwise reports back that a confirmation email was sent.</summary>
    public void SignUp(string email, string password, System.Action<bool, string> done)
    {
        var req = NewReq();
        req.RequestCompleted += (result, code, headers, body) =>
        {
            req.QueueFree();
            try
            {
                var json = Json.ParseString(body.GetStringFromUtf8());
                if (code is >= 200 and < 300 && json.VariantType == Variant.Type.Dictionary)
                {
                    var d = json.AsGodotDictionary();
                    if (d.ContainsKey("access_token")) { ApplySession(d); done(true, "Account created."); return; }
                    done(true, "Account created — check your email to confirm, then log in.");
                    return;
                }
                string msg = ErrorMessage(json, code);
                done(false, msg);
            }
            catch { done(false, "Sign-up failed — check your connection."); }
        };
        string bodyStr = Json.Stringify(new Godot.Collections.Dictionary { ["email"] = email, ["password"] = password });
        req.Request($"{Url}/auth/v1/signup", AuthHeaders(), HttpClient.Method.Post, bodyStr);
    }

    public void SignIn(string email, string password, System.Action<bool, string> done)
    {
        var req = NewReq();
        req.RequestCompleted += (result, code, headers, body) =>
        {
            req.QueueFree();
            try
            {
                var json = Json.ParseString(body.GetStringFromUtf8());
                if (code is >= 200 and < 300 && json.VariantType == Variant.Type.Dictionary)
                {
                    ApplySession(json.AsGodotDictionary());
                    done(true, "Logged in.");
                    return;
                }
                done(false, ErrorMessage(json, code));
            }
            catch { done(false, "Login failed — check your connection."); }
        };
        string bodyStr = Json.Stringify(new Godot.Collections.Dictionary { ["email"] = email, ["password"] = password });
        req.Request($"{Url}/auth/v1/token?grant_type=password", AuthHeaders(), HttpClient.Method.Post, bodyStr);
    }

    private void ApplySession(Godot.Collections.Dictionary d)
    {
        AccessToken = d.TryGetValue("access_token", out var a) ? a.AsString() : null;
        RefreshToken = d.TryGetValue("refresh_token", out var r) ? r.AsString() : null;
        if (d.TryGetValue("user", out var uv) && uv.VariantType == Variant.Type.Dictionary)
        {
            var u = uv.AsGodotDictionary();
            UserId = u.TryGetValue("id", out var id) ? id.AsString() : null;
            UserEmail = u.TryGetValue("email", out var em) ? em.AsString() : null;
        }
        SaveAccount();
    }

    private static string ErrorMessage(Variant json, long code)
    {
        if (json.VariantType == Variant.Type.Dictionary)
        {
            var d = json.AsGodotDictionary();
            if (d.TryGetValue("error_description", out var ed)) return ed.AsString();
            if (d.TryGetValue("msg", out var m)) return m.AsString();
            if (d.TryGetValue("message", out var m2)) return m2.AsString();
        }
        return $"Request failed ({code}).";
    }

    /// <summary>Upload the current save as this account's cloud row (upsert).
    /// Fire-and-forget — never blocks, never surfaces an error to the player.</summary>
    public void PushSave(string saveJson)
    {
        if (!LoggedIn) return;
        var req = NewReq();
        req.RequestCompleted += (result, code, headers, body) => req.QueueFree();
        string[] headers = {
            "apikey: " + AnonKey, "Authorization: Bearer " + AccessToken,
            "Content-Type: application/json", "Prefer: resolution=merge-duplicates",
        };
        string bodyStr = Json.Stringify(new Godot.Collections.Dictionary
        {
            ["user_id"] = UserId ?? "", ["data"] = Json.ParseString(saveJson), ["updated_at"] = Time.GetDatetimeStringFromSystem(true),
        });
        req.Request($"{Url}/rest/v1/saves?on_conflict=user_id", headers, HttpClient.Method.Post, bodyStr);
    }

    /// <summary>Fetch this account's cloud save, if any. done(found, jsonOrNull).</summary>
    public void PullSave(System.Action<bool, string?> done)
    {
        if (!LoggedIn) { done(false, null); return; }
        var req = NewReq();
        req.RequestCompleted += (result, code, headers, body) =>
        {
            req.QueueFree();
            try
            {
                if (code != 200) { done(false, null); return; }
                var arr = Json.ParseString(body.GetStringFromUtf8());
                if (arr.VariantType != Variant.Type.Array || arr.AsGodotArray().Count == 0) { done(false, null); return; }
                var row = arr.AsGodotArray()[0].AsGodotDictionary();
                if (!row.TryGetValue("data", out var dataVal)) { done(false, null); return; }
                done(true, Json.Stringify(dataVal));
            }
            catch { done(false, null); }
        };
        req.Request($"{Url}/rest/v1/saves?user_id=eq.{UserId}&select=data", AuthHeaders(AccessToken));
    }
}
