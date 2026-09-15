using Godot;

namespace Sentinel.UI;

/// <summary>
/// Transparent touch stick. The touch zone is the control's whole rect, but the
/// base ring is docked at a fixed spot near the bottom-left of that zone and is
/// always drawn (dim when idle, bright when held) — so the player always knows
/// where to put their thumb instead of the ring popping up wherever they first
/// touch. A touch anywhere in the zone still drives it: the knob tracks the
/// finger, offset from the fixed base and clamped to <see cref="_maxRadius"/>.
/// Emits a direction (magnitude 0..1) via <see cref="OnMove"/>; zero on release.
/// </summary>
public sealed partial class VirtualJoystick : Control
{
    public System.Action<Vector2>? OnMove;

    private const float _maxRadius = 96f;
    private const float _baseInsetX = 150f;   // fixed-base offset from the zone's left edge
    private const float _baseInsetBottom = 170f;   // fixed-base offset from the zone's bottom edge
    private bool _active;
    private int _touchId = -1;
    private Vector2 _center;      // fixed dock position (same coordinate frame as incoming event.Position)
    private Vector2 _knob;
    private bool _baseReady;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetProcess(true);
    }

    /// <summary>(Re)computes the fixed dock position once the control has a real size.</summary>
    private void EnsureBase()
    {
        if (_baseReady && Size.Y > 0f) return;
        if (Size.Y <= 0f) return;
        _center = Position + new Vector2(_baseInsetX, Size.Y - _baseInsetBottom);
        _knob = _center;
        _baseReady = true;
    }

    public override void _GuiInput(InputEvent e)
    {
        EnsureBase();
        switch (e)
        {
            case InputEventScreenTouch t when t.Pressed && !_active:
                _active = true; _touchId = t.Index;
                var off0 = t.Position - _center;
                if (off0.Length() > _maxRadius) off0 = off0.Normalized() * _maxRadius;
                _knob = _center + off0;
                OnMove?.Invoke(off0 / _maxRadius);
                QueueRedraw();
                AcceptEvent();
                break;
            case InputEventScreenTouch t when !t.Pressed && _active && t.Index == _touchId:
                End();
                AcceptEvent();
                break;
            case InputEventScreenDrag d when _active && d.Index == _touchId:
                var off = d.Position - _center;
                if (off.Length() > _maxRadius) off = off.Normalized() * _maxRadius;
                _knob = _center + off;
                OnMove?.Invoke(off / _maxRadius);
                QueueRedraw();
                AcceptEvent();
                break;
            // mouse fallback for desktop testing
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                if (mb.Pressed && !_active)
                {
                    _active = true; _touchId = -2;
                    var mo0 = mb.Position - _center;
                    if (mo0.Length() > _maxRadius) mo0 = mo0.Normalized() * _maxRadius;
                    _knob = _center + mo0;
                    OnMove?.Invoke(mo0 / _maxRadius);
                    QueueRedraw(); AcceptEvent();
                }
                else if (!mb.Pressed && _active && _touchId == -2) { End(); AcceptEvent(); }
                break;
            case InputEventMouseMotion mm when _active && _touchId == -2:
                var mo = mm.Position - _center;
                if (mo.Length() > _maxRadius) mo = mo.Normalized() * _maxRadius;
                _knob = _center + mo;
                OnMove?.Invoke(mo / _maxRadius);
                QueueRedraw();
                break;
        }
    }

    private void End()
    {
        _active = false; _touchId = -1;
        _knob = _center;
        OnMove?.Invoke(Vector2.Zero);
        QueueRedraw();
    }

    public override void _Draw()
    {
        EnsureBase();
        if (!_baseReady) return;
        var c = _center - Position;   // control-local
        var k = _knob - Position;
        var accent = new Color(0.26f, 0.82f, 0.87f);
        float a = _active ? 1f : 0.4f;   // dim, always-visible dock; brighter while held
        DrawCircle(c, _maxRadius, new Color(accent, 0.06f * a));
        DrawArc(c, _maxRadius, 0, Mathf.Tau, 40, new Color(accent, 0.35f * a), 2.5f);
        DrawArc(c, _maxRadius * 0.42f, 0, Mathf.Tau, 28, new Color(accent, 0.18f * a), 1.5f);
        DrawCircle(k, 30f, new Color(accent, 0.18f * a));
        DrawCircle(k, 22f, new Color(accent, 0.55f * a));
        DrawArc(k, 22f, 0, Mathf.Tau, 24, new Color(1, 1, 1, 0.7f * a), 2f);
    }
}
