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
