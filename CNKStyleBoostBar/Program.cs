using GameOverlay.Drawing;
using GameOverlay.Windows;
using WindowCapture;
using WindowCapture.window_capture;

using PixelColor = (byte r, byte g, byte b, byte a);

namespace CNKStyleBoostBar;
internal class Program
{
    private static void Main(string[] args)
    {
        var meterApp = new CNKStyleBoostMeter();
        meterApp.StartCaptureAndOverlay();
        Console.ReadLine();
        meterApp.StopCaptureAndOverlay();
    }
}
