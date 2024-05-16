namespace WindowCapture;
public class FrameCapturedEventArgs(byte[] rgbaValues, uint width, uint height) : EventArgs
{
    public byte[] RgbaValues { get; } = rgbaValues;
    public uint Width { get; } = width;
    public uint Height { get; } = height;
}
