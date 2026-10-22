using System;
using System.Collections.Generic;
using Tileworks.Kit.UI;
using Tileworks.Kit.Unity;
using TileMatch.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TileMatch.View
{
    public sealed class ShopView : MonoBehaviour, IShopView
    {
        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;
        [SerializeField, ReadOnlyInInspector] private CanvasGroup _group;
        [SerializeField, ReadOnlyInInspector] private RectTransform _panel;
        [SerializeField, ReadOnlyInInspector] private Button[] _items = new Button[0];
        [SerializeField, ReadOnlyInInspector] private TMP_Text[] _labels = new TMP_Text[0];
        [SerializeField, ReadOnlyInInspector] private Button _close;

        private PopupMotion _popup;

        private IShopHandler _handler;

        public bool IsOpen => _popup.IsOpen;

        public void Bind(IShopHandler handler) => _handler = handler;

        private readonly TextNumbers _text = new TextNumbers();

        private void Awake()
        {
            _popup = new PopupMotion(_uiMotion, _group, _panel);
            for (var i = 0; i < _items.Length; i++)
            {
                var index = i;
                _items[i].onClick.AddListener(() =>
                {
                    _uiMotion.Punch(_items[index].transform);
                    _handler?.OnBuy(index);
                });
            }

            _close.onClick.AddListener(Hide);
            _popup.HideNow();
        }

        public void Show(IReadOnlyList<ShopEntry> entries)
        {
            for (var i = 0; i < _items.Length; i++)
            {
                var visible = i < entries.Count;
                _items[i].gameObject.SetActive(visible);
                if (!visible) continue;
                _text.Clear().Append(entries[i].Title).Append("   ").Append(entries[i].Cost).Apply(_labels[i]);
                _items[i].interactable = entries[i].Affordable;
            }

            // A purchase refreshes the open shop; only opening it plays the reveal.
            if (IsOpen) return;
            _popup.Open();
            var shown = 0;
            foreach (var item in _items)
            {
                if (item.gameObject.activeSelf) _uiMotion.PopIn(item.transform, _uiMotion.StaggerDelay(shown++));
            }
        }

        public void Hide() => _popup.Close();
    }
}
