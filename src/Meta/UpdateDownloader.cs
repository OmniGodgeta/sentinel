using Godot;

namespace Sentinel.Meta;

/// <summary>
/// Downloads a release APK in-app (with progress) and hands it straight to the
/// system package installer on Android — via <c>JavaClassWrapper.Wrap</c> calling
/// the small Kotlin helper in <c>android/overlay/UpdateInstaller.kt</c>, no full
/// Godot Android Plugin needed. On any other platform (desktop dev builds) this
/// just downloads the file and reveals it — there's nothing to "install".
/// Requires the custom Gradle export (see CLAUDE.md's in-app updater note and
/// <c>tools/setup_android_gradle.sh</c>) — the FileProvider + REQUEST_INSTALL_PACKAGES
/// permission plain APK export can't add.
/// </summary>
public sealed partial class UpdateDownloader : Node
{
    private const string DownloadPath = "user://update.apk";

    public static UpdateDownloader Start(Node parent, string apkUrl,
        System.Action<float>? onProgress, System.Action<bool, string>? onDone)
    {
        var node = new UpdateDownloader();
        parent.AddChild(node);
        node.Begin(apkUrl, onProgress, onDone);
        return node;
    }

    private HttpRequest? _req;
    private System.Action<float>? _onProgress;
    private System.Action<bool, string>? _onDone;
    private bool _done;

    private void Begin(string url, System.Action<float>? onProgress, System.Action<bool, string>? onDone)
    {
        _onProgress = onProgress;
        _onDone = onDone;

        // a stale partial download from a previous failed attempt would otherwise
        // confuse HttpRequest's resume-on-206 logic
        if (FileAccess.FileExists(DownloadPath)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(DownloadPath));

        _req = new HttpRequest { UseThreads = true, Timeout = 180, DownloadFile = DownloadPath };
        AddChild(_req);
        _req.RequestCompleted += OnCompleted;
        string[] headers = { "User-Agent: Beyond-Game" };
        if (_req.Request(url, headers) != Error.Ok) Finish(false, "couldn't start the download");
    }

    public override void _Process(double delta)
    {
        if (_req == null || _done) return;
        long total = _req.GetBodySize();
        if (total > 0) _onProgress?.Invoke((float)_req.GetDownloadedBytes() / total);
    }

    private void OnCompleted(long result, long code, string[] headers, byte[] body)
    {
        if (result != (long)HttpRequest.Result.Success || code != 200)
        {
            Finish(false, $"download failed ({result}/{code})");
            return;
        }

        string realPath = ProjectSettings.GlobalizePath(DownloadPath);
        if (OS.GetName() == "Android")
        {
            bool ok = TryInstallAndroid(realPath);
            Finish(ok, ok ? "" : "couldn't hand the file to the installer");
        }
        else
        {
            // desktop dev builds: nothing to install, just surface where it landed
            OS.ShellOpen(realPath.GetBaseDir());
            Finish(true, realPath);
        }
    }

    private static bool TryInstallAndroid(string path)
    {
        try
        {
            var cls = JavaClassWrapper.Wrap("com.godot.game.UpdateInstaller");
            if (cls == null) return false;
            var result = cls.Call("install", path);
            return result.AsBool();
        }
        catch
        {
            return false;
        }
    }

    private void Finish(bool ok, string info)
    {
        _done = true;
        _onDone?.Invoke(ok, info);
        QueueFree();
    }
}
