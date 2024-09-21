using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowCapture;

public class FrameCapture : IDisposable
{
    private const int DEFAULT_BUF_SIZE_MB = 128; // This is large enough to hold RGBA values for an 8K window

    public bool IsCapturing { get; private set; }
    public long LeftSearchBound { get; private set; }
    public long TopSearchBound { get; private set; }
    public long RightSearchBound { get; private set; }
    public long BottomSearchBound { get; private set; }

    public float BoostBar1 { get; private set; }
    public float BoostBar2 { get; private set; }
    public float BoostBar3 { get; private set; }

    private bool StopCaptureOnNextFrame;
    private readonly int BufSize;
    private readonly nint BufPtr;
    private byte[] ManagedFrameData;

    private readonly double[] FpsTimestamps;
    private int FpsTimestampsIndex;

    public event EventHandler<FrameCapturedEventArgs>? FrameReady;

    public FrameCapture(int bufSizeMB = DEFAULT_BUF_SIZE_MB)
    {
        // Set some default values
        IsCapturing = false;
        StopCaptureOnNextFrame = false;
        ManagedFrameData = [];
        FpsTimestamps = new double[60];
        FpsTimestampsIndex = 0;

        // Convert the buffer size to bytes and then allocate a block of unmanaged memory for the Rust library to write into
        BufSize = bufSizeMB * 1024 * 1024;
        BufPtr = Marshal.AllocHGlobal(BufSize);
    }

    public void StartCapture(string windowName)
    {
        if (!IsCapturing)
        {
            // Start capturing the desired window and set/clear the appropriate flags
            WindowCapture.StartCapture(windowName, BufPtr, (nuint)BufSize, OnFrameReady, OnStopped, OnPercentagesCalculated, OnSearchAreaDetermined);
            IsCapturing = true;
            StopCaptureOnNextFrame = false;
        }
        else
        {
            throw new InvalidOperationException("Window capture was already started.");
        }
    }

    public void StopCapture()
    {
        if (!IsCapturing)
        {
            throw new InvalidOperationException("Window capture cannot be stopped since it was never started.");
        }
        else
        {
            const int StepTimeToSleepMillis = 10;
            const int TotalTimeToWaitMillis = 1000;

            // Signal to the capture thread that it should stop after the next frame and then wait until capturing stops
            StopCaptureOnNextFrame = true;
            var totalTimeWaited = 0;
            while (IsCapturing)
            {
                if (totalTimeWaited >= TotalTimeToWaitMillis)
                {
                    IsCapturing = false;
                    throw new TimeoutException("No new frames captured within the timeout period to properly stop recording.");
                }

                Thread.Sleep(StepTimeToSleepMillis);
                totalTimeWaited += StepTimeToSleepMillis;
            }
        }
    }

    public double GetFPS()
    {
        var deltaSum = 0.0;
        var numIterations = FpsTimestamps.Length - 1;

        // Iterate through the timestamps starting from the oldest which requires calculating a true index based on the current index for inserting timestamps
        // Sum all of the deltas so an average can be calculated
        // Also, since deltas are calculated in pairs, iteration should stop at the 2nd to last timestamp
        for (var iRaw = 0; iRaw < numIterations; iRaw++)
        {
            var iTrue1 = (FpsTimestampsIndex + iRaw) % FpsTimestamps.Length;
            var iTrue2 = (FpsTimestampsIndex + iRaw + 1) % FpsTimestamps.Length;
            var delta = FpsTimestamps[iTrue2] - FpsTimestamps[iTrue1];
            deltaSum += delta;
        }

        // Calculate the average delta in seconds between frames and then invert it to get the average FPS
        var avgDelta = deltaSum / numIterations;
        var fps = 1.0 / avgDelta;
        return fps;
    }

    private static double GetTimestampInSeconds() => 1.0 * Stopwatch.GetTimestamp() / Stopwatch.Frequency;

    private unsafe bool OnFrameReady(nuint numBytes, uint width, uint height)
    {
        // Read the bytes from the unmanaged memory into a byte array
        if (ManagedFrameData.Length != (int)numBytes)
        {
            ManagedFrameData = new byte[numBytes];
        }

        Marshal.Copy(BufPtr, ManagedFrameData, 0, (int)numBytes);

        FpsTimestamps[FpsTimestampsIndex] = GetTimestampInSeconds();
        FpsTimestampsIndex = (FpsTimestampsIndex + 1) % FpsTimestamps.Length;

        // Create the event args and invoke the event
        var eventArgs = new FrameCapturedEventArgs(ManagedFrameData, width, height);
        FrameReady?.Invoke(this, eventArgs);

        return StopCaptureOnNextFrame;
    }

    private int OnStopped()
    {
        // Set the appropriate boolean and then return a value of 0 which is unused anyways
        IsCapturing = false;
        return 0;
    }

    private bool OnPercentagesCalculated(float boost1, float boost2, float boost3)
    {
        BoostBar1 = boost1;
        BoostBar2 = boost2;
        BoostBar3 = boost3;
        return StopCaptureOnNextFrame;
    }

    private int OnSearchAreaDetermined(long leftMost, long topMost, long rightMost, long bottomMost)
    {
        LeftSearchBound = leftMost;
        TopSearchBound = topMost;
        RightSearchBound = rightMost;
        BottomSearchBound = bottomMost;
        return 0;
    }

    public void Dispose()
    {
        // Stop the Rust window capture if it's running and free the unmanaged memory
        if (IsCapturing)
        {
            StopCapture();
        }
        Marshal.FreeHGlobal(BufPtr);
        GC.SuppressFinalize(this);
    }
}
