namespace CNKStyleBoostBar;
public class DisplayInformation
{
    /// <value>The screen width for the system on which the UI element sizes were originally determined.</value>
    public const int BaseScreenWidth = 1920;
    /// <value>The screen height for the system on which the UI element sizes were originally determined.</value>
    public const int BaseScreenHeight = 1080;
    /// <value>The width of the desktop.</value>
    public int SystemWidth { get; set; }
    /// <value>The height of the desktop.</value>
    public int SystemHeight { get; set; }
    /// <value>The width at which the game is rendered.</value>
    public int RenderWidth { get; set; }
    /// <value>The height at which the game is rendered.</value>
    public int RenderHeight { get; set; }

    public int SystemToRenderOffsetX => (SystemWidth - RenderWidth) / 2;
    public int SystemToRenderOffsetY => (SystemHeight - RenderHeight) / 2;

    private static float GetUIScaleXOrY(float baseDimension, float systemDimension, float renderDimension, float viewportDimension) => systemDimension / baseDimension * (viewportDimension / renderDimension);

    /// <summary>
    /// Gets the scale in the X direction to make UI elements look the same size as the display on which they were originally created.
    /// </summary>
    public float GetUIScaleX(float viewportWidth) => GetUIScaleXOrY(BaseScreenWidth, SystemWidth, RenderWidth, viewportWidth);

    /// <summary>
    /// Gets the scale in the Y direction to make UI elements look the same size as the display on which they were originally created.
    /// </summary>
    public float GetUIScaleY(float viewportHeight) => GetUIScaleXOrY(BaseScreenHeight, SystemHeight, RenderHeight, viewportHeight);

    /// <summary>
    /// Gets the minimum of <see cref="GetUIScaleX(float)"/> and <see cref="GetUIScaleY(float)"/> to try best fitting UI elements on different viewports.
    /// </summary>
    public float GetUIScale(float viewportWidth, float viewportHeight) => Math.Min(GetUIScaleX(viewportWidth), GetUIScaleY(viewportHeight));
}
