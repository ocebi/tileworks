using Tileworks.Kit.UI;
using Tileworks.Kit.Unity;
using TileMatch.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TileMatch.View
{
    public sealed class StatsView : MonoBehaviour, IStatsView
    {
        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;
        [SerializeField, ReadOnlyInInspector] private CanvasGroup _group;
        [SerializeField, ReadOnlyInInspector] private RectTransform _panel;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _title;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _body;
        [SerializeField, ReadOnlyInInspector] private Button _close;

        private PopupMotion _popup;

        private void Awake()
        {
            _popup = new PopupMotion(_uiMotion, _group, _panel);
            _close.onClick.AddListener(Hide);
            _popup.HideNow();
        }

        public void Show(string title, string body)
        {
            _title.text = title;
            _body.text = body;
            _popup.Open();
        }

        public void Hide() => _popup.Close();
    }
}
