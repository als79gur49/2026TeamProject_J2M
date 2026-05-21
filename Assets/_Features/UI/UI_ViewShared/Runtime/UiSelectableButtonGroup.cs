using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.ViewShared
{
    [Serializable]
    public sealed class UiSelectableButtonGroup
    {
        [SerializeField] private UiSelectionVisualProfile _visualProfile;
        [SerializeField] private UiSelectableButtonSlot[] _slots = Array.Empty<UiSelectableButtonSlot>();
        [SerializeField] private bool _wrap;
        [SerializeField] private bool _skipNonInteractable = true;
        [SerializeField] private int _selectedIndex;

        public int SelectedIndex => _selectedIndex;

        public int SlotCount => _slots != null ? _slots.Length : 0;

        public bool IsConfigured
        {
            get
            {
                if (_slots == null || _slots.Length == 0)
                {
                    return false;
                }

                for (var i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] == null || !_slots[i].IsConfigured)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public UiSelectableButtonSlot GetSlot(int index)
        {
            if (_slots == null || index < 0 || index >= _slots.Length)
            {
                return null;
            }

            return _slots[index];
        }

        public void Configure(
            UiSelectableButtonSlot[] slots,
            UiSelectionVisualProfile visualProfile,
            bool wrap,
            bool skipNonInteractable,
            int selectedIndex = 0)
        {
            _slots = slots ?? Array.Empty<UiSelectableButtonSlot>();
            _visualProfile = visualProfile;
            _wrap = wrap;
            _skipNonInteractable = skipNonInteractable;
            SetSelectedIndex(selectedIndex);
        }

        public void ValidateOrThrow(string message)
        {
            if (!IsConfigured || _visualProfile == null)
            {
                throw new InvalidOperationException(message);
            }
        }

        public bool TryMove(int delta)
        {
            if (delta == 0 || !IsConfigured)
            {
                return false;
            }

            var nextIndex = FindNextSelectableIndex(_selectedIndex, delta);
            if (nextIndex < 0 || nextIndex == _selectedIndex)
            {
                return false;
            }

            _selectedIndex = nextIndex;
            RefreshVisuals();
            return true;
        }

        public bool SetSelectedIndex(int index)
        {
            if (!IsConfigured)
            {
                _selectedIndex = Mathf.Max(0, index);
                RefreshVisuals();
                return false;
            }

            var clamped = Mathf.Clamp(index, 0, _slots.Length - 1);
            if (!IsSelectable(clamped))
            {
                var selectable = FindNextSelectableIndex(clamped, 1);
                if (selectable < 0)
                {
                    RefreshVisuals();
                    return false;
                }

                clamped = selectable;
            }

            var changed = _selectedIndex != clamped;
            _selectedIndex = clamped;
            RefreshVisuals();
            return changed;
        }

        public bool SetSelectedIndexSilently(int index)
        {
            if (!IsConfigured)
            {
                var previousIndex = _selectedIndex;
                _selectedIndex = Mathf.Max(0, index);
                return previousIndex != _selectedIndex;
            }

            var clamped = Mathf.Clamp(index, 0, _slots.Length - 1);
            if (!IsSelectable(clamped))
            {
                var selectable = FindNextSelectableIndex(clamped, 1);
                if (selectable < 0)
                {
                    return false;
                }

                clamped = selectable;
            }

            var changed = _selectedIndex != clamped;
            _selectedIndex = clamped;
            return changed;
        }

        public Button GetSelectedButton()
        {
            var slot = GetSlot(_selectedIndex);
            return slot != null ? slot.Button : null;
        }

        public void PlaySelectedSubmitFeedback()
        {
            GetSlot(_selectedIndex)?.ResolveSelectionFeedback()?.PlaySubmitFeedback();
        }

        public void RefreshVisuals()
        {
            if (_slots == null)
            {
                return;
            }

            var profile = _visualProfile;
            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                var frame = slot != null ? slot.SelectionFrame : null;
                var isSelected = i == _selectedIndex;
                ApplySelectionFeedback(slot, isSelected);
                if (frame == null)
                {
                    continue;
                }

                if (profile != null && profile.FrameSprite != null)
                {
                    frame.sprite = profile.FrameSprite;
                }

                frame.color = profile != null
                    ? (isSelected ? profile.SelectedFrameColor : profile.UnselectedFrameColor)
                    : (isSelected ? Color.white : new Color(1f, 1f, 1f, 0f));
                frame.gameObject.SetActive(isSelected || profile == null || !profile.HideUnselectedFrames);
            }
        }

        public void HideAllFrames()
        {
            if (_slots == null)
            {
                return;
            }

            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                ApplySelectionFeedback(slot, focused: false);

                var frame = slot != null ? slot.SelectionFrame : null;
                if (frame == null)
                {
                    continue;
                }

                var profile = _visualProfile;
                frame.color = profile != null ? profile.UnselectedFrameColor : new Color(1f, 1f, 1f, 0f);
                frame.gameObject.SetActive(false);
            }
        }

        private int FindNextSelectableIndex(int origin, int delta)
        {
            if (_slots == null || _slots.Length == 0)
            {
                return -1;
            }

            var index = Mathf.Clamp(origin, 0, _slots.Length - 1);
            for (var step = 0; step < _slots.Length; step++)
            {
                index += delta;
                if (_wrap)
                {
                    if (index < 0)
                    {
                        index = _slots.Length - 1;
                    }
                    else if (index >= _slots.Length)
                    {
                        index = 0;
                    }
                }
                else if (index < 0 || index >= _slots.Length)
                {
                    return -1;
                }

                if (IsSelectable(index))
                {
                    return index;
                }
            }

            return -1;
        }

        private bool IsSelectable(int index)
        {
            var button = GetSlot(index)?.Button;
            return button != null &&
                   button.gameObject.activeInHierarchy &&
                   (!_skipNonInteractable || button.interactable);
        }

        private static void ApplySelectionFeedback(UiSelectableButtonSlot slot, bool focused)
        {
            slot?.ResolveSelectionFeedback()?.SetNavigationFocused(focused);
        }
    }
}
