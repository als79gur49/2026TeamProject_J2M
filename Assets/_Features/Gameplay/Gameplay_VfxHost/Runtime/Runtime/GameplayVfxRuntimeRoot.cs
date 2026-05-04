using System;
using UnityEngine;

namespace Game.Feature.Gameplay.Vfx.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayVfxRuntimeRoot : MonoBehaviour
    {
        private const string OneShotRootName = "OneShot";
        private const string PersistentRootName = "Persistent";
        private const string TailRootName = "Tail";
        private const string PoolRootName = "Pool";

        [SerializeField] private Transform oneShotRoot;
        [SerializeField] private Transform persistentRoot;
        [SerializeField] private Transform tailRoot;
        [SerializeField] private Transform poolRoot;

        public Transform OneShotRoot => oneShotRoot;

        public Transform PersistentRoot => persistentRoot;

        public Transform TailRoot => tailRoot;

        public Transform PoolRoot => poolRoot;

        public static GameplayVfxRuntimeRoot CreateUnder(
            Transform parent,
            string name = "GameplayVfxRuntimeRoot")
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var rootObject = new GameObject(name);
            rootObject.transform.SetParent(parent, worldPositionStays: false);
            var root = rootObject.AddComponent<GameplayVfxRuntimeRoot>();
            root.InitializeRuntime();
            return root;
        }

        public void InitializeRuntime()
        {
            oneShotRoot = EnsureChild(oneShotRoot, OneShotRootName);
            persistentRoot = EnsureChild(persistentRoot, PersistentRootName);
            tailRoot = EnsureChild(tailRoot, TailRootName);
            poolRoot = EnsureChild(poolRoot, PoolRootName);
        }

        private void Awake()
        {
            InitializeRuntime();
        }

        private Transform EnsureChild(Transform current, string childName)
        {
            if (current != null)
            {
                return current;
            }

            var existing = transform.Find(childName);
            if (existing != null)
            {
                return existing;
            }

            var childObject = new GameObject(childName);
            childObject.transform.SetParent(transform, worldPositionStays: false);
            return childObject.transform;
        }
    }
}
