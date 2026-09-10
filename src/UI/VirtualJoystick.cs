using Godot;

namespace Sentinel.UI;

/// <summary>
/// Transparent floating touch stick. The touch zone is the control's whole rect;
/// the ring appears where the finger lands and the knob tracks it, clamped to
/// <see cref="_maxRadius"/>. Emits a direction (magnitude 0..1) via
/// <see cref="OnMove"/>; zero on release.
/// </summary>
public sealed partial class VirtualJoystick : Control
{
    public System.Action<Vector2>? OnMove;

    private const float _maxRadius = 96f;
    private bool _active;
    private int _touchId = -1;
    private Vector2 _center;
    private Vector2 _knob;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetProcess(true);
    }

    public override void _GuiInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventScreenTouch t when t.Pressed && !_active:
                _active = true; _touchId = t.Index;
                _center = t.Position; _knob = t.Position;
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
                if (mb.Pressed && !_active) { _active = true; _touchId = -2; _center = mb.Position; _knob = mb.Position; QueueRedraw(); AcceptEvent(); }
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
        OnMove?.Invoke(Vector2.Zero);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_active) return;
        var c = _center - Position;   // control-local
        var k = _knob - Position;
        var accent = new Color(0.26f, 0.82f, 0.87f);
        DrawCircle(c, _maxRadius, new Color(accent, 0.06f));
        DrawArc(c, _maxRadius, 0, Mathf.Tau, 40, new Color(accent, 0.35f), 2.5f);
        DrawArc(c, _maxRadius * 0.42f, 0, Mathf.Tau, 28, new Color(accent, 0.18f), 1.5f);
        DrawCircle(k, 30f, new Color(accent, 0.18f));
        DrawCircle(k, 22f, new Color(accent, 0.55f));
        DrawArc(k, 22f, 0, Mathf.Tau, 24, new Color(1, 1, 1, 0.7f), 2f);
    }
}
