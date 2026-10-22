using PrimeTween;
using Tileworks.Kit.Unity;
using UnityEngine;

namespace Tileworks.Kit.UI
{
    // Plays a menu screen's entrance when it appears: its pieces pop in one after another, then the idle loops start and a
    // sparkle drifts up now and then. With less motion the pieces are simply there and nothing loops.
    public sealed class HomeEntrance : MonoBehaviour
    {
        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;
        [SerializeField, ReadOnlyInInspector, Tooltip("Pop in one after another, in this order.")] private Transform[] _popIns = new Transform[0];
        [SerializeField, ReadOnlyInInspector, Tooltip("Breathe once the pop-ins have settled; pivoted at their feet.")] private Transform[] _breathers = new Transform[0];
        [SerializeField, ReadOnlyInInspector, Tooltip("Stretched over the screen; its jitter spreads the sparkles.")] private UiBurst _sparkles;

        private bool _idle;
        private float _nextSparkle;

        private void Start()
        {
            for (var i = 0; i < _popIns.Length; i++) _uiMotion.PopIn(_popIns[i], _uiMotion.StaggerDelay(i));
            if (_uiMotion.Reduced) Idle();
            else Tween.Delay(this, _uiMotion.StaggerDelay(_popIns.Length) + _uiMotion.Config.PopDuration, static (HomeEntrance e) => e.Idle(), useUnscaledTime: true);
        }

        private void Idle()
        {
            _idle = true;
            foreach (var breather in _breathers) _uiMotion.Breathe(breather);
            _nextSparkle = Time.unscaledTime + _uiMotion.Config.SparkleEvery;
        }

        private void Update()
        {
            var every = _uiMotion.Config.SparkleEvery;
            if (!_idle || _sparkles == null || every <= 0f || _uiMotion.Reduced || Time.unscaledTime < _nextSparkle) return;
            _nextSparkle = Time.unscaledTime + every;
            _sparkles.Burst(_sparkles.transform.position);
        }

        private void OnDestroy()
        {
            Tween.StopAll(this);
            foreach (var popIn in _popIns) if (popIn != null) Tween.StopAll(popIn);
            foreach (var breather in _breathers) if (breather != null) Tween.StopAll(breather);
        }
    }
}
