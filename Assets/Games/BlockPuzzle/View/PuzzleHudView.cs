using System;
using BlockPuzzle.Controller;
using BlockPuzzle.Model;
using Tileworks.Kit.UI;
using Tileworks.Kit.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.View
{
    public sealed class PuzzleHudView : MonoBehaviour, IPuzzleHudView
    {
        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _level;
        [SerializeField, ReadOnlyInInspector] private GameObject _replayTag;
        [SerializeField, ReadOnlyInInspector] private TMP_Text _goal;
        [SerializeField, ReadOnlyInInspector] private RectTransform _progress;
        [SerializeField, ReadOnlyInInspector] private Button _pause;

        private BlockGoal _goalKind;
        private int _target;
        private bool _goalShown;

        private IPuzzleHudHandler _handler;

        private readonly TextNumbers _text = new TextNumbers();

        private void Awake() => _pause.onClick.AddListener(() => _handler?.OnPause());

        public void Bind(IPuzzleHudHandler handler) => _handler = handler;

        public void SetLevel(int number) => _text.Set(_level, "Level ", number);

        public void SetReplay(bool replay) => _replayTag.SetActive(replay);

        public void SetGoal(BlockGoal goal, int gems, int scoreTarget)
        {
            _goalKind = goal;
            _target = goal == BlockGoal.Gems ? gems : scoreTarget;
            _goalShown = false;
            if (goal == BlockGoal.Gems) SetGemsLeft(gems);
            else SetScore(0);
            _goalShown = true;
        }

        public void SetScore(int score)
        {
            if (_goalKind != BlockGoal.Score) return;
            _text.Clear().Append(score).Append(" / ").Append(_target).Apply(_goal);
            SetProgress(_target <= 0 ? 1f : score / (float)_target);
        }

        public void SetGemsLeft(int gems)
        {
            if (_goalKind != BlockGoal.Gems) return;
            if (gems == 1) _goal.text = "1 gem left";
            else _text.Clear().Append(gems).Append(" gems left").Apply(_goal);
            SetProgress(_target <= 0 ? 1f : 1f - gems / (float)_target);
        }

        public void SetInteractable(bool interactable) => _pause.interactable = interactable;

        // The first value of a level is set at once; later changes fill the bar and punch the goal text.
        private void SetProgress(float progress)
        {
            if (!_goalShown)
            {
                _progress.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
                return;
            }

            _uiMotion.Fill(_progress, progress);
            _uiMotion.Punch(_goal.transform);
        }
    }
}
