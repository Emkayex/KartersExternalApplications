namespace WindowCapture;
public class BoostPercentagesReadyEventArgs(float boost1, float boost2, float boost3, int frameWidth, int frameHeight) : EventArgs
{
    public float Boost1 { get; } = boost1;
    public float Boost2 { get; } = boost2;
    public float Boost3 { get; } = boost3;
    public int FrameWidth { get; } = frameWidth;
    public int FrameHeight { get; } = frameHeight;
}
