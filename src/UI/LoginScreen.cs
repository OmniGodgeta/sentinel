using Godot;
using Sentinel.Game;
using Sentinel.Meta;

namespace Sentinel.UI;

/// <summary>
/// Once-per-launch popup right after the splash: log in / create an account /
/// skip and play as a guest. Entirely optional — guests keep using the local
/// user://save.json exactly like before. Built so a reinstall (the user's own
/// "I deleted the app to update and lost my save" complaint) doesn't lose
/// progress: logging in pulls the cloud save if one exists for that account.
/// </summary>
public sealed partial class LoginScreen : CanvasLayer
{
    public AppRoot App = null!;
    public System.Action? Done;

    private LineEdit _email = null!, _password = null!;
    private Label _status = null!;
    private bool _leaving;

    public override void _Ready()
    {
        Layer = 31;
        AddChild(new MenuBackground { ShowPlanet = true, PlanetY = 0.3f, PlanetScale = 0.7f, NebulaAlpha = 0.35f });

        var panel = new PanelContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -260, OffsetRight = 260, OffsetTop = -260, OffsetBottom = 260,
        };
        panel.Theme = UiTheme.Instance;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.05f, 0.09f, 0.97f),
            BorderColor = new Color(UiTheme.Accent, 0.5f),
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
            ContentMarginLeft = 22, ContentMarginRight = 22, ContentMarginTop = 20, ContentMarginBottom = 20,
        });
        AddChild(panel);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 12);
        panel.AddChild(col);

        var title = new Label { Text = "COMMANDER ACCOUNT", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontOverride("font", UiTheme.Display);
        title.AddThemeFontSizeOverride("font_size", 34);
        title.AddThemeColorOverride("font_color", UiTheme.Accent);
        col.AddChild(title);

        var sub = new Label
        {
            Text = "Log in so your save survives a reinstall — totally optional.",
            HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color(1, 1, 1, 0.65f),
        };
        sub.AddThemeFontSizeOverride("font_size", 20);
        col.AddChild(sub);

        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

        _email = new LineEdit { PlaceholderText = "email", CustomMinimumSize = new Vector2(0, 78) };
        _email.AddThemeFontSizeOverride("font_size", 25);
        col.AddChild(_email);

        _password = new LineEdit { PlaceholderText = "password (6+ chars)", Secret = true, CustomMinimumSize = new Vector2(0, 78) };
        _password.AddThemeFontSizeOverride("font_size", 25);
        _password.TextSubmitted += _ => Attempt(signUp: false);
        col.AddChild(_password);

        _status = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(1, 0.9f, 0.6f) };
        _status.AddThemeFontSizeOverride("font_size", 20);
        col.AddChild(_status);

        var loginBtn = new Button { Text = "Log In", CustomMinimumSize = new Vector2(0, 87) };
        loginBtn.AddThemeFontSizeOverride("font_size", 27);
        UiTheme.StylePrimary(loginBtn);
        loginBtn.Pressed += () => Attempt(signUp: false);
        col.AddChild(loginBtn);

        var signupBtn = new Button { Text = "Create Account", CustomMinimumSize = new Vector2(0, 78) };
        signupBtn.AddThemeFontSizeOverride("font_size", 24);
        signupBtn.Pressed += () => Attempt(signUp: true);
        col.AddChild(signupBtn);

        var skip = new Button { Text = "Skip — play as guest", CustomMinimumSize = new Vector2(0, 70), Flat = true };
        skip.AddThemeFontSizeOverride("font_size", 21);
        skip.Modulate = new Color(1, 1, 1, 0.6f);
        skip.Pressed += Leave;
        col.AddChild(skip);

        if (CloudSave.Instance.LoggedIn)
        {
            _status.Text = $"Signed in as {CloudSave.Instance.UserEmail} — syncing…";
            PullThenLeave();
        }
    }

    private void Attempt(bool signUp)
    {
        string email = _email.Text.Trim();
        string pass = _password.Text;
        if (email.Length < 3 || !email.Contains('@')) { _status.Text = "enter a valid email"; return; }
        if (pass.Length < 6) { _status.Text = "password needs 6+ characters"; return; }
        _status.Text = "working…";
        System.Action<bool, string> done = (ok, msg) =>
        {
            if (_leaving) return;
            _status.Text = msg;
            if (ok && CloudSave.Instance.LoggedIn) PullThenLeave();
        };
        if (signUp) CloudSave.Instance.SignUp(email, pass, done);
        else CloudSave.Instance.SignIn(email, pass, done);
    }

    private void PullThenLeave()
    {
        CloudSave.Instance.PullSave((found, json) =>
        {
            if (_leaving) return;
            if (found && json != null) App.AdoptCloudSave(json);
            else App.Save.Save();   // first login on this account — seed the cloud row
            Leave();
        });
    }

    private void Leave()
    {
        if (_leaving) return;
        _leaving = true;
        Done?.Invoke();
        QueueFree();
    }
}
