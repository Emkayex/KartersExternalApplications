using GameOverlay.Drawing;

namespace CNKStyleBoostBar;
class BoostMeterData
{
    public readonly float[] BoostAmounts = new float[3];
    public float ArcStartAngle { get; set; } = 45f;
    public float ArcEndAngle { get; set; } = -30f;
    public float DriftDirection { get; set; } = 1f;

    public readonly Func<float, SolidBrush> GetMeterColorForFillPercentFunc;
    public readonly Func<DisplayInformation> DisplayInfoGetter;
    private readonly Func<BrushCollection?> BrushesGetter;

    public float ViewportWidth => DisplayInfoGetter().RenderWidth;
    public float ViewportHeight => DisplayInfoGetter().RenderHeight;

    public BoostMeterData(Func<float, SolidBrush> getMeterColorForFillPercentFunc, Func<BrushCollection?> brushesGetter, Func<DisplayInformation> displayInfoGetter)
    {
        GetMeterColorForFillPercentFunc = getMeterColorForFillPercentFunc;
        BrushesGetter = brushesGetter;
        DisplayInfoGetter = displayInfoGetter;
    }

    public (int?, float?) GetBoostNumberAndValue()
    {
        for (var i = BoostAmounts.Length - 1; i >= 0; i--)
        {
            var boostAmount = BoostAmounts[i];
            if (boostAmount > 0)
            {
                return (i, boostAmount);
            }
        }

        return (null, null);
    }

    public void DrawBoostBar(Graphics gfx, BoostBarStyle style, int boostNum, float baseX, float baseY)
    {
        // Get the boost value to use for the meter
        var boostValue = BoostAmounts[boostNum];

        if (style == BoostBarStyle.Rectangle)
        {
            DrawRectangleBoostBars(gfx, boostValue, boostNum, baseX, baseY);
        }
        else if ((style == BoostBarStyle.ArcsSameAngles) || (style == BoostBarStyle.ArcsSameLength))
        {
            DrawArcBoostBars(gfx, style, boostValue, boostNum, baseX, baseY);
        }
    }

    private SolidBrush GetMeterBrushColor(float boostValue) => GetMeterColorForFillPercentFunc(boostValue);

    private void DrawRectangleBoostBars(Graphics gfx, float boostValue, int boostNum, float baseX, float baseY)
    {
        const float BaseBoostBarWidth = 20f;
        const float BaseBoostBarHeight = 100f;
        const float BaseSpacingBetweenBars = 10f;
        const float BaseSpaceBetweenStartOfBars = BaseBoostBarWidth + BaseSpacingBetweenBars;

        // Calculate the boost bar width, height, and spacing based on the needed UI element scale for different display sizes and multiplayer
        var uiScale = DisplayInfoGetter().GetUIScaleY(ViewportHeight);
        var boostBarWidth = BaseBoostBarWidth * uiScale;
        var boostBarHeight = BaseBoostBarHeight * uiScale;
        var spaceBetweenStartOfBars = BaseSpaceBetweenStartOfBars * uiScale;

        // Calculate a local X for this boost bar where the middle bar should be centered
        // The positioning in the Y-direction doesn't need to change depending on the direction of the drift
        var centeredBoostNum = (BoostAmounts.Length - 1) / 2f;
        var boostOffsetFromCenter = boostNum - centeredBoostNum;
        var rectLocalX = (boostBarWidth / -2f) + (boostOffsetFromCenter * spaceBetweenStartOfBars);
        if (DriftDirection < 0)
        {
            // Flip the direction when drifting in the negative X-direction to fill the bars in "opposite" order
            rectLocalX *= -1;
            rectLocalX -= boostBarWidth;
        }

        // Calculate an offset from the player's base point in the X and Y directions
        var xOffset = ViewportWidth / 10f * DriftDirection;
        var yOffset = -2f * boostBarHeight;

        // Calculate the X and Y positions at which the bar should be drawn
        var x = baseX + rectLocalX + xOffset;
        var y = baseY + yOffset;

        // Draw the boost bar (with a gray background)
        var rect = Rectangle.Create(x, y, boostBarWidth, boostBarHeight);
        gfx.FillRectangle(BrushesGetter()!.Gray, rect);
        gfx.DrawHorizontalProgressBar(BrushesGetter()!.Black, GetMeterBrushColor(boostValue), rect, 3f * uiScale, boostValue / CNKStyleBoostMeter.MaxValueForBoost * 100f);
    }

    private void DrawArcBoostBars(Graphics gfx, BoostBarStyle style, float boostValue, int boostNum, float baseX, float baseY)
    {
        const float baseInnerRadius = 75f - 10f;
        const float baseOuterRadius = 95f - 10f;

        // Calculate some base values based off the config options
        var baseStartAngle = ArcStartAngle;
        var baseEndAngle = ArcEndAngle;
        var baseAngleDelta = baseEndAngle - baseStartAngle;
        var baseOuterArcProduct = baseOuterRadius * baseAngleDelta;

        // Get a scaling factor for the UI elements
        var uiScale = DisplayInfoGetter().GetUIScaleY(ViewportHeight);

        // Calculate the radii for the arc based on the boost number
        var extraRadius = boostNum * (30f * uiScale);
        var innerRadius = (baseInnerRadius * uiScale) + extraRadius;
        var outerRadius = (baseOuterRadius * uiScale) + extraRadius;

        // Calculate the start and end angle based on the direction being faced
        var (startAngle, endAngle) = (baseStartAngle, baseEndAngle);
        if (DriftDirection < 0)
        {
            startAngle = 180f - startAngle;
            endAngle = 180f - endAngle;
        }

        // Calculate the angle delta necessary to make this arc length equal to the base arc length
        if (style == BoostBarStyle.ArcsSameLength)
        {
            var neededAngleDelta = baseOuterArcProduct / outerRadius;
            var amountToSubtractOffStartAndEnd = (baseAngleDelta - neededAngleDelta) / 2f;
            startAngle += amountToSubtractOffStartAndEnd * DriftDirection;
            endAngle -= amountToSubtractOffStartAndEnd * DriftDirection;
        }
        var angleDelta = endAngle - startAngle;

        // Calculate the center of the circle from which the arc originates
        var xOffset = ViewportWidth / 25f * DriftDirection;
        var yOffset = ViewportHeight / 5f;
        var center = new Point(baseX + xOffset, baseY - yOffset);

        // Draw the boost bar as an arc
        var fullArcGeo = CreateArcDegrees(gfx, center, innerRadius, outerRadius, startAngle, endAngle);
        var fillArcGeo = CreateArcDegrees(gfx, center, innerRadius, outerRadius, startAngle, startAngle + (angleDelta * boostValue / CNKStyleBoostMeter.MaxValueForBoost));
        gfx.FillGeometry(fullArcGeo, BrushesGetter()!.Gray);
        gfx.FillGeometry(fillArcGeo, GetMeterBrushColor(boostValue));
        gfx.DrawGeometry(fullArcGeo, BrushesGetter()!.Black, 3f * uiScale);
    }

    private static Geometry CreateArcDegrees(Graphics gfx, Point center, float innerRadius, float outerRadius, float startAngle, float endAngle)
    {
        return CreateArcRadians(
            gfx: gfx,
            center: center,
            innerRadius: innerRadius,
            outerRadius: outerRadius,
            startAngle: startAngle * MathF.PI / 180f,
            endAngle: endAngle * MathF.PI / 180f
        );
    }

    private static Geometry CreateArcRadians(Graphics gfx, Point center, float innerRadius, float outerRadius, float startAngle, float endAngle)
    {
        static Point RadiusAndAngleToPoint(float radius, float angle) => new(radius * MathF.Cos(angle * -1f), radius * MathF.Sin(angle * -1f));

        static Point AddPoints(Point pt1, Point pt2) => new(pt1.X + pt2.X, pt1.Y + pt2.Y);

        // Calculate the four points used to draw the arc by converting from polar coordinates to cartesian coordinates
        var pointsNoOffset = new Point[]
        {
            RadiusAndAngleToPoint(innerRadius, startAngle),
            RadiusAndAngleToPoint(innerRadius, endAngle),
            RadiusAndAngleToPoint(outerRadius, endAngle),
            RadiusAndAngleToPoint(outerRadius, startAngle)
        };

        // Add the points to the circle center to offset them correctly
        var points = pointsNoOffset.Select(pt => AddPoints(center, pt)).ToArray();

        var (drawInnerRadius, drawOuterRadius) = (innerRadius, outerRadius * -1f);
        if (endAngle < startAngle)
        {
            drawInnerRadius *= -1f;
            drawOuterRadius *= -1f;
        }

        // Create and return the Geometry object
        var geo = gfx.CreateGeometry();
        geo.BeginFigure(points[0], true);
        geo.AddCurve(points[1], drawInnerRadius);
        geo.AddPoint(points[2]);
        geo.AddCurve(points[3], drawOuterRadius);
        geo.AddPoint(points[0]);
        geo.EndFigure();
        geo.Close();
        return geo;
    }
}
