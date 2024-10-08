﻿using System.Diagnostics;

namespace WindowCapture;

public class FrameCapture : IDisposable
{
    public bool IsCapturing { get; private set; }
    public long LeftSearchBound { get; private set; }
    public long TopSearchBound { get; private set; }
    public long RightSearchBound { get; private set; }
    public long BottomSearchBound { get; private set; }

    private bool StopCaptureOnNextFrame;

    private readonly double[] FpsTimestamps;
    private int FpsTimestampsIndex;

    public event EventHandler<BoostPercentagesReadyEventArgs>? BoostPercentagesReady;

    public FrameCapture()
    {
        // Set some default values
        IsCapturing = false;
        StopCaptureOnNextFrame = false;
        FpsTimestamps = new double[60];
        FpsTimestampsIndex = 0;
    }

    public void StartCapture(string windowName)
    {
        if (!IsCapturing)
        {
            // Start capturing the desired window and set/clear the appropriate flags
            WindowCapture.StartCapture(windowName, OnStopped, OnPercentagesCalculated, OnSearchAreaDetermined);
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

    private int OnStopped()
    {
        // Set the appropriate boolean and then return a value of 0 which is unused anyways
        IsCapturing = false;
        return 0;
    }

    private bool OnPercentagesCalculated(float boost1, float boost2, float boost3, uint frameWidth, uint frameHeight)
    {
        FpsTimestamps[FpsTimestampsIndex] = GetTimestampInSeconds();
        FpsTimestampsIndex = (FpsTimestampsIndex + 1) % FpsTimestamps.Length;

        var eventArgs = new BoostPercentagesReadyEventArgs(boost1, boost2, boost3, (int)frameWidth, (int)frameHeight);
        BoostPercentagesReady?.Invoke(this, eventArgs);
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
        GC.SuppressFinalize(this);
    }
}
