using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.UI;
using Tileworks.Kit.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tileworks.Tiles.View
{
    // A small question over whatever opened it, such as "Reset progress?". AskAsync completes with the player's choice; cancelling
    // the token closes it with no choice made.
    public sealed class ConfirmPopup : MonoBehaviour
    {
        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;
        [SerializeField, ReadOnlyInInspector] private CanvasGroup _group;
        [SerializeField, ReadOnlyInInspector] private RectTransform _panel;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _title;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _body;
        [SerializeField, ReadOnlyInInspector] private Button _confirm;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _confirmLabel;
        [SerializeField, ReadOnlyInInspector] private Button _cancel;

        private PopupMotion _popup;

        private void Awake()
        {
            _popup = new PopupMotion(_uiMotion, _group, _panel);
            _popup.HideNow();
        }

        public async UniTask<bool> AskAsync(string title, string body, string confirm, CancellationToken ct)
        {
            _title.text = title;
            _body.text = body;
            _confirmLabel.text = confirm;
            _popup.Open();
            // The first button pressed wins; cancelling the popup's token releases the other.
            using var popup = CancellationTokenSource.CreateLinkedTokenSource(ct);
            try
            {
                return await UniTask.WhenAny(_confirm.OnClickAsync(popup.Token), _cancel.OnClickAsync(popup.Token)) == 0;
            }
            finally
            {
                popup.Cancel();
                if (_group != null) _popup.Close();
            }
        }
    }
}
