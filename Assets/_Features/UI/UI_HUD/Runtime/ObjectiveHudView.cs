using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.Feature.UI.HUD
{
    public sealed class ObjectiveHudView : MonoBehaviour
    {
        private const string ActiveStateName = "Active";
        private const string InactiveStateName = "Inactive";
        private const int BaseLayerIndex = 0;

        [SerializeField] private GameObject _root;
        [SerializeField] private RectTransform _objectiveListRoot;
        [SerializeField] private RectTransform _objectiveItemTemplate;

        private readonly List<ObjectiveItemBinding> _itemPool = new List<ObjectiveItemBinding>();
        private readonly HashSet<string> _animatedSatisfiedStableIds = new HashSet<string>(StringComparer.Ordinal);
        private ObjectiveHudViewModel _viewModel;

        public ObjectiveHudViewModel ViewModel => _viewModel;

        public void Bind(ObjectiveHudViewModel viewModel)
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            _viewModel = viewModel;
            if (_viewModel != null)
            {
                _viewModel.Changed += HandleViewModelChanged;
            }

            RefreshView();
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_root, nameof(_root));
            RequireReference(_objectiveListRoot, nameof(_objectiveListRoot));
            RequireReference(_objectiveItemTemplate, nameof(_objectiveItemTemplate));

            if (_objectiveItemTemplate.transform.parent != _objectiveListRoot)
            {
                throw new InvalidOperationException($"{nameof(ObjectiveHudView)} item template must be a direct child of Objective_List.");
            }

            if (_objectiveItemTemplate.GetComponent<Animator>() == null)
            {
                throw new InvalidOperationException($"{nameof(ObjectiveHudView)} item template is missing an Animator.");
            }

            if (!HasObjectiveLabel(_objectiveItemTemplate.gameObject))
            {
                throw new InvalidOperationException($"{nameof(ObjectiveHudView)} item template is missing Label_Objective.");
            }
        }

        private void OnEnable()
        {
            RefreshView();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_objectiveListRoot, nameof(_objectiveListRoot));
            ValidateSerializedReference(_objectiveItemTemplate, nameof(_objectiveItemTemplate));
        }
#endif

        private void OnDestroy()
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
        }

        private void RefreshView()
        {
            ValidateAuthoredStructureOrThrow();
            HideAuthoredListChildren();

            var isVisible = _viewModel != null && _viewModel.IsVisible;
            _root.SetActive(isVisible);
            if (!isVisible)
            {
                DeactivatePooledItems();
                _animatedSatisfiedStableIds.Clear();
                return;
            }

            var rows = _viewModel.Rows;
            while (_itemPool.Count < rows.Count)
            {
                _itemPool.Add(CreateItem(_itemPool.Count));
            }

            for (var i = 0; i < _itemPool.Count; i++)
            {
                var active = i < rows.Count;
                var item = _itemPool[i];
                item.Root.SetActive(active);
                if (!active)
                {
                    continue;
                }

                BindItem(item, rows[i]);
            }
        }

        private ObjectiveItemBinding CreateItem(int index)
        {
            var itemTransform = Instantiate(_objectiveItemTemplate, _objectiveListRoot);
            itemTransform.name = $"Objective_Item_Runtime_{index:00}";
            itemTransform.gameObject.SetActive(false);
            return new ObjectiveItemBinding(itemTransform.gameObject);
        }

        private void BindItem(
            ObjectiveItemBinding item,
            ObjectiveConditionHudViewModel row)
        {
            item.Label.text = row.Text;

            if (!row.IsSatisfied)
            {
                _animatedSatisfiedStableIds.Remove(row.StableId);
                PlayAnimatorState(item.Animator, InactiveStateName);
                return;
            }

            if (row.JustSatisfied)
            {
                PlayAnimatorState(item.Animator, ActiveStateName);
                _animatedSatisfiedStableIds.Add(row.StableId);
            }
        }

        private void HideAuthoredListChildren()
        {
            if (_objectiveListRoot == null)
            {
                return;
            }

            for (var i = 0; i < _objectiveListRoot.childCount; i++)
            {
                var child = _objectiveListRoot.GetChild(i).gameObject;
                if (IsPooledItem(child))
                {
                    continue;
                }

                child.SetActive(false);
            }
        }

        private bool IsPooledItem(GameObject child)
        {
            for (var i = 0; i < _itemPool.Count; i++)
            {
                if (ReferenceEquals(_itemPool[i].Root, child))
                {
                    return true;
                }
            }

            return false;
        }

        private void DeactivatePooledItems()
        {
            for (var i = 0; i < _itemPool.Count; i++)
            {
                _itemPool[i].Root.SetActive(false);
            }
        }

        private static void PlayAnimatorState(Animator animator, string stateName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            animator.Play(stateName, BaseLayerIndex, 0.0f);
            animator.Update(0.0f);
        }

        private static bool HasObjectiveLabel(GameObject root)
        {
            var labels = root.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                if (labels[i].name == "Label_Objective")
                {
                    return true;
                }
            }

            return false;
        }

        private static void RequireReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{nameof(ObjectiveHudView)} is missing authored reference '{fieldName}'.");
            }
        }

#if UNITY_EDITOR
        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(ObjectiveHudView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif

        private sealed class ObjectiveItemBinding
        {
            public ObjectiveItemBinding(GameObject root)
            {
                Root = root ?? throw new ArgumentNullException(nameof(root));
                Animator = root.GetComponent<Animator>();
                Label = FindLabel(root);
            }

            public GameObject Root { get; }

            public Animator Animator { get; }

            public TMP_Text Label { get; }

            private static TMP_Text FindLabel(GameObject root)
            {
                var labels = root.GetComponentsInChildren<TMP_Text>(true);
                for (var i = 0; i < labels.Length; i++)
                {
                    if (labels[i].name == "Label_Objective")
                    {
                        return labels[i];
                    }
                }

                if (labels.Length > 0)
                {
                    return labels[0];
                }

                throw new InvalidOperationException($"{nameof(ObjectiveHudView)} item '{root.name}' is missing a TMP label.");
            }
        }
    }
}
