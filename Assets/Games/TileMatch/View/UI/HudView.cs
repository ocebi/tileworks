using Tileworks.Kit.UI;
using Tileworks.Kit.Unity;
using TileMatch.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TileMatch.View
{
    // Level pill and pause; the boosters under the board are the shared BoosterBarView.
    public sealed class HudView : MonoBehaviour, IHudView
    {
        [SerializeField, ReadOnlyInInspector] private TMP_Text _level;
        [SerializeField, ReadOnlyInInspector] private GameObject _replayTag;
        [SerializeField, ReadOnlyInInspector] private Button _pause;

        private IHudHandler _handler;

        private readonly TextNumbers _text = new TextNumbers();

        private void Awake() => _pause.onClick.AddListener(() => _handler?.OnPause());

        public void SetLevel(int number) => _text.Set(_level, "Level ", number);

        public void SetReplay(bool replay) => _replayTag.SetActive(replay);

        public void SetInteractable(bool interactable) => _pause.interactable = interactable;

        public void Bind(IHudHandler handler) => _handler = handler;
    }
}
