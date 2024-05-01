﻿using WindowCapture.window_capture;

using PixelColor = (byte r, byte g, byte b, byte a);

namespace CNKStyleBoostBar;
internal class Program
{
    private static readonly PixelColor GrayTuple = (0x5F, 0x5E, 0x5F, 0xFF);
    private static readonly PixelColor RedTuple = (0xF5, 0x00, 0x00, 0xFF);
    private static readonly double[] AreaSamplePercentages = [0.1, 0.5, 0.9];

    private static double[] LatestBoostValues = [0, 0, 0];

    private static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
    }

    private static void OnFrameCaptured(object? sender, FrameCapturedEventArgs e)
    {
        // Use only the lower portion of the screen from 75% width and height to the edges to reduce the number of pixels to process
        var areaLeftBound = (int)(e.Width * 0.75);
        var areaTopBound = (int)(e.Height * 0.75);

        // Iterate through all of the pixels and find the boundaries of gray and red pixels from the boost bars
        // The values are stored in the array as R, G, B, A, R, G, B, A, ...
        // Therefore, pixels must be processed as groups of four bytes
        var leftMost = (int)e.Width;
        var rightMost = areaLeftBound;
        var topMost = (int)e.Height;
        var bottomMost = areaTopBound;
        for (var xRaw = areaLeftBound; xRaw < e.Width; xRaw++)
        {
            for (var yRaw = areaTopBound; yRaw < e.Width; yRaw++)
            {
                // For the given pixel identified by (xRaw, yRaw), calculate the array index if all RGBA values were stored as 32-bit uints
                // Then multiply the index by four to get the correct index for the R byte
                var indexRaw = (yRaw * e.Width) + xRaw;
                var rIndex = (int)(indexRaw * 4);
                var r = e.RgbaValues[rIndex];
                var g = e.RgbaValues[rIndex + 1];
                var b = e.RgbaValues[rIndex + 2];
                var a = e.RgbaValues[rIndex + 3];
                if (IsGray(r, g, b, a) || IsRed(r, g, b, a))
                {
                    leftMost = Math.Min(xRaw, leftMost);
                    rightMost = Math.Max(xRaw, rightMost);
                    topMost = Math.Min(yRaw, topMost);
                    bottomMost = Math.Max(yRaw, bottomMost);
                }
            }
        }

        // If the selected area width or height is not positive, clear the latest boost values and then return
        var width = rightMost - leftMost;
        var height = bottomMost - topMost;
        if ((width <= 0) || (height <= 0))
        {
            LatestBoostValues = [0, 0, 0];
            return;
        }

        // The bars can be sampled at the 10%, 50%, and 90% X-positions within the selected area to count the red and gray pixels
        // A black bar is missing where the fill bar cuts off, but that will be counted as a red pixel instead of a gray pixel
        for (var i = 0; i < LatestBoostValues.Length; i++)
        {
            var percent = AreaSamplePercentages[i];
            var xRaw = (int)(leftMost * (width * percent));

            var grayCount = 0;
            var redCount = 0;
            for (var yRaw = 0; yRaw < height; yRaw++)
            {
                // Calculate the index of the red byte for an RGBA value
                var indexRaw = (yRaw * e.Width) + xRaw;
                var rIndex = (int)(indexRaw * 4);

                // Retrieve the RGBA values and check if the pixel is gray
                // Anything other than gray will be counted as red since those should be the only two colors present besides the black bar
                var r = e.RgbaValues[rIndex];
                var g = e.RgbaValues[rIndex + 1];
                var b = e.RgbaValues[rIndex + 2];
                var a = e.RgbaValues[rIndex + 3];
                if (IsGray(r, g, b, a))
                {
                    grayCount++;
                }
                else
                {
                    redCount++;
                }
            }

            // Calculate the portion that's filled and store it to the appropriate array position
            var portionFilled = 1.0 * redCount / (redCount + grayCount);
            LatestBoostValues[i] = portionFilled;
        }
    }

    private static bool IsGray(byte r, byte g, byte b, byte a) => IsPixelColor(r, g, b, a, GrayTuple);
    private static bool IsRed(byte r, byte g, byte b, byte a) => IsPixelColor(r, g, b, a, RedTuple);
    private static bool IsPixelColor(byte r, byte g, byte b, byte a, PixelColor color) => (r == color.r) && (g == color.g) && (b == color.b) && (a == color.a);
}
