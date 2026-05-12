using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.ViewShared
{
    public enum UiFocusRegion
    {
        Header = 0,
        Audio = 1,
        Display = 2,
        Input = 3,
    }

    public enum UiFocusNodeKind
    {
        Button = 0,
        Toggle = 1,
        Slider = 2,
        Dropdown = 3,
        Hotspot = 4,
    }

    public enum UiFocusMoveResult
    {
        NotHandled = 0,
        Moved = 1,
        AdjustedValue = 2,
        EnteredEditMode = 3,
        ExitedEditMode = 4,
        Submitted = 5,
    }

    [Serializable]
    public readonly struct UiFocusNodeId : IEquatable<UiFocusNodeId>
    {
        [SerializeField] private readonly string _value;

        public UiFocusNodeId(string value)
        {
            _value = value ?? string.Empty;
        }

        public string Value => _value ?? string.Empty;

        public bool Equals(UiFocusNodeId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is UiFocusNodeId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }
    }

    public interface IUiFocusableControlAdapter
    {
        bool IsInteractable { get; }

        bool Activate();

        bool Adjust(int delta);
    }

    public sealed class UiDelegateFocusAdapter : IUiFocusableControlAdapter
    {
        private readonly Func<int, bool> _adjust;
        private readonly Func<bool> _isInteractable;
        private readonly Func<bool> _submit;

        public UiDelegateFocusAdapter(Func<bool> submit, Func<int, bool> adjust = null, Func<bool> isInteractable = null)
        {
            _submit = submit;
            _adjust = adjust;
            _isInteractable = isInteractable;
        }

        public bool IsInteractable => _isInteractable == null || _isInteractable();

        public bool Activate()
        {
            return IsInteractable && _submit != null && _submit();
        }

        public bool Adjust(int delta)
        {
            return IsInteractable && _adjust != null && _adjust(delta);
        }
    }

    [Serializable]
    public sealed class UiFocusNodeSlot
    {
        public string Id;
        public UiFocusRegion Region;
        public UiFocusNodeKind Kind;
        public int Row;
        public int Column;
        public Image SelectionFrame;
        public UiSelectionVisualProfile VisualProfile;

        public UiFocusNodeId NodeId => new UiFocusNodeId(Id);
    }

    public sealed class UiFocusGraphNavigator
    {
        private readonly List<Node> _nodes = new();
        private readonly Dictionary<UiFocusRegion, Node> _lastContentByRegion = new();
        private UiFocusRegion _visibleContentRegion = UiFocusRegion.Audio;
        private Node _current;
        private Node _lastContent;
        private bool _isEditing;

        public UiFocusNodeId CurrentNodeId => _current != null ? _current.Id : new UiFocusNodeId();

        public bool IsEditing => _isEditing;

        public void Clear()
        {
            HideCurrent();
            _nodes.Clear();
            _lastContentByRegion.Clear();
            _current = null;
            _lastContent = null;
            _isEditing = false;
        }

        public void AddNode(UiFocusNodeSlot slot, IUiFocusableControlAdapter adapter)
        {
            if (slot == null || string.IsNullOrEmpty(slot.Id))
            {
                return;
            }

            _nodes.Add(new Node(slot, adapter));
        }

        public void SetVisibleContentRegion(UiFocusRegion region)
        {
            _visibleContentRegion = region;
            if (_current == null || !IsVisible(_current))
            {
                Focus(GetLastContentNode(region) ?? GetFirstContentNode(region) ?? GetNode("Header.Back") ?? GetFirstVisibleNode());
            }
        }

        public void FocusContentRegion(UiFocusRegion region, bool restoreLast)
        {
            _visibleContentRegion = region;
            var target = restoreLast ? GetLastContentNode(region) : null;
            Focus(target ?? GetFirstContentNode(region) ?? GetNode("Header.Back") ?? GetFirstVisibleNode());
        }

        public void Focus(UiFocusNodeId id)
        {
            Focus(GetNode(id.Value));
        }

        public void FocusFirst()
        {
            Focus(GetLastContentNode(_visibleContentRegion) ?? GetFirstContentNode(_visibleContentRegion) ?? GetNode("Header.Back") ?? GetFirstVisibleNode());
        }

        public UiFocusMoveResult Navigate(UiNavigationCommand command)
        {
            if (_current == null)
            {
                FocusFirst();
                return _current != null ? UiFocusMoveResult.Moved : UiFocusMoveResult.NotHandled;
            }

            if (_isEditing)
            {
                if (command == UiNavigationCommand.Left || command == UiNavigationCommand.Down)
                {
                    return _current.Adapter != null && _current.Adapter.Adjust(-1)
                        ? UiFocusMoveResult.AdjustedValue
                        : UiFocusMoveResult.NotHandled;
                }

                if (command == UiNavigationCommand.Right || command == UiNavigationCommand.Up)
                {
                    return _current.Adapter != null && _current.Adapter.Adjust(1)
                        ? UiFocusMoveResult.AdjustedValue
                        : UiFocusMoveResult.NotHandled;
                }
            }

            var target = ResolveMove(command);
            if (target == null || ReferenceEquals(target, _current))
            {
                return UiFocusMoveResult.NotHandled;
            }

            Focus(target);
            return UiFocusMoveResult.Moved;
        }

        public UiFocusMoveResult Submit()
        {
            if (_current == null)
            {
                FocusFirst();
            }

            if (_current == null)
            {
                return UiFocusMoveResult.NotHandled;
            }

            if (_isEditing)
            {
                _isEditing = false;
                RefreshCurrentVisual();
                return UiFocusMoveResult.ExitedEditMode;
            }

            if (_current.Kind == UiFocusNodeKind.Slider || _current.Kind == UiFocusNodeKind.Dropdown)
            {
                _isEditing = true;
                RefreshCurrentVisual();
                return UiFocusMoveResult.EnteredEditMode;
            }

            return _current.Adapter != null && _current.Adapter.Activate()
                ? UiFocusMoveResult.Submitted
                : UiFocusMoveResult.NotHandled;
        }

        public UiFocusMoveResult Cancel()
        {
            if (!_isEditing)
            {
                return UiFocusMoveResult.NotHandled;
            }

            _isEditing = false;
            RefreshCurrentVisual();
            return UiFocusMoveResult.ExitedEditMode;
        }

        public void HideCurrent()
        {
            if (_current != null)
            {
                ApplyFrame(_current, selected: false, editing: false);
            }
        }

        public void HideAllFrames()
        {
            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (node.Frame == null)
                {
                    continue;
                }

                node.Frame.color = node.Profile != null ? node.Profile.UnselectedFrameColor : new Color(1f, 1f, 1f, 0f);
                node.Frame.gameObject.SetActive(false);
            }
        }

        private Node ResolveMove(UiNavigationCommand command)
        {
            if (_current.Id.Value == "Header.Back" && command == UiNavigationCommand.Down)
            {
                var lastContent = GetLastContentNode(_visibleContentRegion);
                return lastContent != null && IsVisible(lastContent)
                    ? lastContent
                    : GetFirstContentNode(_visibleContentRegion);
            }

            if (_current.Region != UiFocusRegion.Header && command == UiNavigationCommand.Up && IsTopContentRow(_current))
            {
                return GetNode("Header.Back");
            }

            if (command == UiNavigationCommand.Up || command == UiNavigationCommand.Down)
            {
                return FindVertical(command == UiNavigationCommand.Down ? 1 : -1);
            }

            return FindHorizontal(command == UiNavigationCommand.Right ? 1 : -1);
        }

        private Node FindVertical(int delta)
        {
            var best = (Node)null;
            for (var i = 0; i < _nodes.Count; i++)
            {
                var candidate = _nodes[i];
                if (!IsVisible(candidate) || candidate.Region != _current.Region)
                {
                    continue;
                }

                var rowDelta = candidate.Row - _current.Row;
                if (Math.Sign(rowDelta) != Math.Sign(delta))
                {
                    continue;
                }

                if (best == null ||
                    Mathf.Abs(rowDelta) < Mathf.Abs(best.Row - _current.Row) ||
                    (Mathf.Abs(rowDelta) == Mathf.Abs(best.Row - _current.Row) &&
                     Mathf.Abs(candidate.Column - _current.Column) < Mathf.Abs(best.Column - _current.Column)))
                {
                    best = candidate;
                }
            }

            return best;
        }

        private Node FindHorizontal(int delta)
        {
            var best = (Node)null;
            for (var i = 0; i < _nodes.Count; i++)
            {
                var candidate = _nodes[i];
                if (!IsVisible(candidate) ||
                    candidate.Region != _current.Region ||
                    candidate.Row != _current.Row)
                {
                    continue;
                }

                var columnDelta = candidate.Column - _current.Column;
                if (Math.Sign(columnDelta) != Math.Sign(delta))
                {
                    continue;
                }

                if (best == null || Mathf.Abs(columnDelta) < Mathf.Abs(best.Column - _current.Column))
                {
                    best = candidate;
                }
            }

            return best;
        }

        private bool IsTopContentRow(Node node)
        {
            for (var i = 0; i < _nodes.Count; i++)
            {
                var candidate = _nodes[i];
                if (IsVisible(candidate) &&
                    candidate.Region == node.Region &&
                    candidate.Row < node.Row)
                {
                    return false;
                }
            }

            return true;
        }

        private Node GetFirstContentNode(UiFocusRegion region)
        {
            Node best = null;
            for (var i = 0; i < _nodes.Count; i++)
            {
                var candidate = _nodes[i];
                if (!IsVisible(candidate) || candidate.Region != region)
                {
                    continue;
                }

                if (best == null || candidate.Row < best.Row || (candidate.Row == best.Row && candidate.Column < best.Column))
                {
                    best = candidate;
                }
            }

            return best;
        }

        private Node GetLastContentNode(UiFocusRegion region)
        {
            return _lastContentByRegion.TryGetValue(region, out var node) && node != null && IsNodeRegistered(node)
                ? node
                : null;
        }

        private Node GetFirstVisibleNode()
        {
            for (var i = 0; i < _nodes.Count; i++)
            {
                if (IsVisible(_nodes[i]))
                {
                    return _nodes[i];
                }
            }

            return null;
        }

        private bool IsNodeRegistered(Node node)
        {
            for (var i = 0; i < _nodes.Count; i++)
            {
                if (ReferenceEquals(_nodes[i], node))
                {
                    return true;
                }
            }

            return false;
        }

        private Node GetNode(string id)
        {
            for (var i = 0; i < _nodes.Count; i++)
            {
                if (string.Equals(_nodes[i].Id.Value, id, StringComparison.Ordinal))
                {
                    return _nodes[i];
                }
            }

            return null;
        }

        private bool IsVisible(Node node)
        {
            return node.Region == UiFocusRegion.Header || node.Region == _visibleContentRegion;
        }

        private void Focus(Node node)
        {
            if (ReferenceEquals(_current, node))
            {
                RefreshCurrentVisual();
                return;
            }

            HideCurrent();
            _current = node;
            _isEditing = false;
            if (_current != null && _current.Region != UiFocusRegion.Header)
            {
                _lastContent = _current;
                _lastContentByRegion[_current.Region] = _current;
            }

            RefreshCurrentVisual();
        }

        private void RefreshCurrentVisual()
        {
            if (_current != null)
            {
                ApplyFrame(_current, selected: true, editing: _isEditing);
            }
        }

        private static void ApplyFrame(Node node, bool selected, bool editing)
        {
            if (node.Frame == null)
            {
                return;
            }

            if (node.Profile != null && node.Profile.FrameSprite != null)
            {
                node.Frame.sprite = node.Profile.FrameSprite;
            }

            node.Frame.gameObject.SetActive(selected || node.Profile == null || !node.Profile.HideUnselectedFrames);
            node.Frame.color = node.Profile != null
                ? (editing ? node.Profile.EditFrameColor : selected ? node.Profile.SelectedFrameColor : node.Profile.UnselectedFrameColor)
                : (selected ? Color.white : new Color(1f, 1f, 1f, 0f));
        }

        private sealed class Node
        {
            public Node(UiFocusNodeSlot slot, IUiFocusableControlAdapter adapter)
            {
                Id = slot.NodeId;
                Region = slot.Region;
                Kind = slot.Kind;
                Row = slot.Row;
                Column = slot.Column;
                Frame = slot.SelectionFrame;
                Profile = slot.VisualProfile;
                Adapter = adapter;
            }

            public UiFocusNodeId Id { get; }

            public UiFocusRegion Region { get; }

            public UiFocusNodeKind Kind { get; }

            public int Row { get; }

            public int Column { get; }

            public Image Frame { get; }

            public UiSelectionVisualProfile Profile { get; }

            public IUiFocusableControlAdapter Adapter { get; }
        }
    }
}
