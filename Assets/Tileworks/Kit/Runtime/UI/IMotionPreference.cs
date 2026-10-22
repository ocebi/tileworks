namespace Tileworks.Kit.UI
{
    // The player's "less motion" choice, read each time a UI animation starts.
    public interface IMotionPreference
    {
        bool ReduceMotion { get; }
    }
}
