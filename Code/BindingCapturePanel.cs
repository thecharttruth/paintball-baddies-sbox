using Sandbox.UI;

namespace PaintballBaddies;

// Reuse the engine's focused keyboard events so arrows cannot become menu navigation.
public sealed class BindingCapturePanel : Panel
{
    public PaintballControls Controls { get; set; }
    public BindingCapturePanel() { AcceptsFocus = true; }

    public override void Tick()
    {
        base.Tick();
        if (Controls?.Capturing is not null && !HasFocus) Focus();
    }

    public override void OnButtonTyped(ButtonEvent e)
    {
        if (Controls?.CaptureButton(e.Button, e.Pressed) == true) { e.StopPropagation = true; return; }
        base.OnButtonTyped(e);
    }

    public override void OnButtonEvent(ButtonEvent e)
    {
        if (Controls?.CaptureButton(e.Button, e.Pressed) == true) { e.StopPropagation = true; return; }
        base.OnButtonEvent(e);
    }
}
