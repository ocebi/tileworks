namespace Tileworks.Meta.Api
{
    // Called inside the level-result transaction, so a feature's progress commits or rolls back with the level.
    public interface ILevelResultListener
    {
        void OnLevelResult(string levelId, bool won);
    }
}
