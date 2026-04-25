using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    internal static class GameplayEnemyViewPrefabInstantiator
    {
        public static GameplayEntityView InstantiateEnemyView(
            GameplayEntityView prefab,
            Transform parent,
            in EntityState entity,
            string ownerDescription)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            var instance = UnityEngine.Object.Instantiate(prefab, parent);
            ResetViewTransform(instance, entity.entityId);
            SanitizePrefabPhysics(instance);
            EnemyViewPrefabRequirements.ValidateEnemyViewInstance(instance, ownerDescription);
            GameplayActionAudioPrefabRequirements.GetOptionalValidatedAuthoring(instance, ownerDescription);
            EnsureEnemyInactiveVisualController(instance, entity);
            return instance;
        }

        private static void ResetViewTransform(GameplayEntityView view, int entityId)
        {
            view.name = $"EntityView_{entityId}";
            view.transform.localPosition = Vector3.zero;
            view.transform.localRotation = Quaternion.identity;
            view.transform.localScale = Vector3.one;
            view.Initialize(entityId);
        }

        private static void EnsureEnemyInactiveVisualController(GameplayEntityView view, in EntityState entity)
        {
            if (view == null ||
                !EntityRolePolicy.IsEnemyUnit(entity) ||
                view.GetComponent<EnemyInactiveVisualController>() != null)
            {
                return;
            }

            view.gameObject.AddComponent<EnemyInactiveVisualController>();
        }

        private static void SanitizePrefabPhysics(GameplayEntityView view)
        {
            if (view == null)
            {
                return;
            }

            var colliders = view.GetComponentsInChildren<Collider>(includeInactive: true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                collider.enabled = false;
                DestroyComponent(collider);
            }

            var rigidbodies = view.GetComponentsInChildren<Rigidbody>(includeInactive: true);
            for (var i = 0; i < rigidbodies.Length; i++)
            {
                var rigidbody = rigidbodies[i];
                if (rigidbody == null)
                {
                    continue;
                }

                rigidbody.detectCollisions = false;
                rigidbody.isKinematic = true;
                DestroyComponent(rigidbody);
            }
        }

        private static void DestroyComponent(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(component);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }
    }
}
