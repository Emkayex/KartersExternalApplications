using GameOverlay.Drawing;
using GameOverlay.Windows;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WindowCapture;

namespace KartersMirrorMode;
internal class Program
{
    private static byte[] LatestImage = [];
    private static readonly GameOverlay.Drawing.Point Anchor = new(0, 0);

    private static void Main(string[] args)
    {
        var gfx = new Graphics
        {
            MeasureFPS = false,
            PerPrimitiveAntiAliasing = false,
            TextAntiAliasing = false
        };

        var window = new GraphicsWindow(0, 0, 1920, 1080)
        {
            FPS = 60,
            IsTopmost = true,
            IsVisible = true
        };

        window.SetupGraphics += Window_SetupGraphics;
        window.DrawGraphics += Window_DrawGraphics;
        window.DestroyGraphics += Window_DestroyGraphics;

        window.Create();

        using var capture = new FrameCapture();
        capture.FrameReady += OnFrameCaptured;

        capture.StartCapture("TheKarters2");
        Console.Error.WriteLine("Press Enter to stop.");
        Console.ReadLine();

        window.Join();
    }

    private static void OnFrameCaptured(object? sender, FrameCapturedEventArgs e)
    {
        var img = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(e.RgbaValues, (int)e.Width, (int)e.Height);
        img.Mutate(x => {
            x.RotateFlip(RotateMode.None, FlipMode.Horizontal);
        });
        using var temp = new MemoryStream();
        img.SaveAsBmp(temp);
        LatestImage = temp.ToArray();
    }

    private static void Window_SetupGraphics(object? sender, SetupGraphicsEventArgs e)
    {

    }

    private static void Window_DrawGraphics(object? sender, DrawGraphicsEventArgs e)
    {
        if (LatestImage.Length <= 0)
        {
            return;
        }

        // Start by clearing the screen
        var gfx = e.Graphics;
        gfx.ClearScene();

        // Get the most recent image and draw it to the screen
        var img = gfx.CreateImage(LatestImage);
        gfx.DrawImage(img, Anchor);
    }

    private static void Window_DestroyGraphics(object? sender, DestroyGraphicsEventArgs e)
    {

    }
}
