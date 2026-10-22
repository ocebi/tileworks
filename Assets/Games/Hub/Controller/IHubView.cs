using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.App;

namespace Hub.Controller
{
    public interface IHubHandler
    {
        void OnCard(int index);
        void OnPlay();
    }

    public interface IHubView
    {
        // Null unbinds.
        void Bind(IHubHandler handler);

        // One card per game, in this order (card indices follow it), opening on `selected`.
        void Open(IReadOnlyList<GameEntry> games, int selected);
        void Select(int index);

        // The selected card was tapped again.
        void Nudge(int index);

        // Locks input and fades the controls out.
        UniTask LeaveAsync();
    }
}
