using GameOverlay.Drawing;

namespace CNKStyleBoostBar;
public class BrushCollection(Graphics gfx) : IDisposable
{
    public readonly SolidBrush Black = gfx.CreateSolidBrush(0, 0, 0);
    public readonly SolidBrush White = gfx.CreateSolidBrush(255, 255, 255);
    public readonly SolidBrush Gray = gfx.CreateSolidBrush(128, 128, 128);
    public readonly SolidBrush Red = gfx.CreateSolidBrush(255, 0, 0);
    public readonly SolidBrush Orange = gfx.CreateSolidBrush(255, 106, 0);
    public readonly SolidBrush Yellow = gfx.CreateSolidBrush(255, 216, 0);
    public readonly SolidBrush Green = gfx.CreateSolidBrush(0, 255, 0);
    public readonly SolidBrush Blue = gfx.CreateSolidBrush(0, 0, 255);
    public readonly SolidBrush Purple = gfx.CreateSolidBrush(178, 0, 255);
    public readonly SolidBrush Transparent = gfx.CreateSolidBrush(0, 0, 0, 0);

    public void Dispose()
    {
        Black.Dispose();
        White.Dispose();
        Gray.Dispose();
        Red.Dispose();
        Orange.Dispose();
        Yellow.Dispose();
        Green.Dispose();
        Blue.Dispose();
        Purple.Dispose();
        Transparent.Dispose();
        GC.SuppressFinalize(this);
    }
}
