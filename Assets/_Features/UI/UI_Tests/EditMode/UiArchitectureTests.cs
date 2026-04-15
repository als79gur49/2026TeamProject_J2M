using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class UiArchitectureTests
    {
        [Test]
        public void OnlyCompositionUiAssemblyReferencesGameplayHostAssembly()
        {
            var hostAssemblyName = typeof(GameplaySceneHost).Assembly.GetName().Name;
            var runtimeUiAssemblies = GetRuntimeUiAssemblies();

            foreach (var assembly in runtimeUiAssemblies.Where(assembly => assembly != typeof(GameplayUiFlowInstaller).Assembly))
            {
                var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
                Assert.That(references, Does.Not.Contain(hostAssemblyName), assembly.GetName().Name);
            }

            var compositionReferences = typeof(GameplayUiFlowInstaller).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            Assert.That(compositionReferences, Does.Contain(hostAssemblyName));
        }

        [Test]
        public void OnlyApplicationAndCompositionUiAssembliesReferenceGameplayUiAccessAssembly()
        {
            var uiAccessAssemblyName = typeof(IGameplayQueryFacade).Assembly.GetName().Name;
            var applicationAssembly = typeof(HUDRootPresenter).Assembly;
            var compositionAssembly = typeof(GameplayUiFlowInstaller).Assembly;

            foreach (var assembly in GetRuntimeUiAssemblies().Where(assembly => assembly != applicationAssembly && assembly != compositionAssembly))
            {
                var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
                Assert.That(references, Does.Not.Contain(uiAccessAssemblyName), assembly.GetName().Name);
            }

            var applicationReferences = applicationAssembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            Assert.That(applicationReferences, Does.Contain(uiAccessAssemblyName));

            var compositionReferences = compositionAssembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            Assert.That(compositionReferences, Does.Contain(uiAccessAssemblyName));
        }

        [Test]
        public void ApplicationUiAssembly_DoesNotReferenceGameplayAssembly()
        {
            var gameplayAssemblyName = typeof(WorldState).Assembly.GetName().Name;
            var references = typeof(HUDRootPresenter).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain(gameplayAssemblyName));
        }

        [Test]
        public void ApplicationUiAssembly_OnlyGameplayUiPresentationSourceDependsOnGameplayPresentationFeed()
        {
            var applicationAssembly = typeof(HUDRootPresenter).Assembly;
            var feedType = typeof(IGameplayPresentationFeed);

            var dependentTypes = applicationAssembly
                .GetTypes()
                .Where(type => !type.IsNested && TypeDependsOn(type, feedType))
                .Select(type => type.FullName)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(
                dependentTypes,
                Is.EqualTo(new[]
                {
                    typeof(GameplayUiPresentationSource).FullName,
                }));
        }

        [Test]
        public void PresenterTypes_DoNotDependOnRawGameplayPresentationContracts()
        {
            var forbiddenTypes = new[]
            {
                typeof(IGameplayPresentationFeed),
                typeof(GameplayPresentationFrame),
                typeof(GameplayPresentationState),
            };
            var presenterTypes = new[]
            {
                typeof(HUDRootPresenter),
                typeof(PlayerStatusPresenter),
                typeof(ActionBarPresenter),
                typeof(NotificationPresenter),
                typeof(ObjectiveStatusPresenter),
                typeof(InventoryScreenPresenter),
                typeof(InventoryCatalogPresenter),
                typeof(InventoryDetailPresenter),
                typeof(InventoryActionPresenter),
            };

            foreach (var presenterType in presenterTypes)
            {
                foreach (var forbiddenType in forbiddenTypes)
                {
                    Assert.That(
                        TypeDependsOn(presenterType, forbiddenType),
                        Is.False,
                        $"{presenterType.FullName} depends on {forbiddenType.FullName}");
                }
            }
        }

        [Test]
        public void FeatureUiAssemblies_DoNotDefineRuntimeOnGuiMethods()
        {
            var offendingMethods = GetRuntimeUiAssemblies()
                .SelectMany(assembly => assembly.GetExportedTypes())
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(method => method.Name == "OnGUI")
                    .Select(method => $"{type.FullName}.{method.Name}"))
                .ToArray();

            Assert.That(offendingMethods, Is.Empty);
        }

        [Test]
        public void HudScreenAndPopupAssemblies_DoNotExposeForbiddenGameplayTypes()
        {
            var forbiddenTypes = new HashSet<Type>
            {
                typeof(WorldState),
                typeof(TickResult),
                typeof(TickRunner),
                typeof(GameplayInputHost),
                typeof(GameplayTickViewPresenter),
            };

            var assemblies = new[]
            {
                typeof(HUDRootView).Assembly,
                typeof(GameplayScreenView).Assembly,
                typeof(PausePopupView).Assembly,
            };

            var leakedTypes = assemblies
                .Distinct()
                .SelectMany(assembly => assembly.GetExportedTypes())
                .SelectMany(GetPublicSurfaceTypes)
                .Select(NormalizeType)
                .Where(type => type != null &&
                               (forbiddenTypes.Contains(type) ||
                                (type.Namespace != null && type.Namespace.StartsWith("Game.Feature.Gameplay", StringComparison.Ordinal))))
                .Distinct()
                .ToArray();

            Assert.That(leakedTypes, Is.Empty);
        }

        [Test]
        public void GameplayQueryFacade_Surface_RemainsBoundedToStageOneReaders()
        {
            var propertyNames = typeof(IGameplayQueryFacade)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(propertyNames, Is.EqualTo(new[] { "Objectives", "PlayerHud", "Session" }));
        }

        [Test]
        public void HUDRootPresenter_DoesNotDependOnGameplayCommandGateway()
        {
            Assert.That(
                TypeDependsOn(typeof(HUDRootPresenter), typeof(IGameplayCommandGateway)),
                Is.False);
        }

        [Test]
        public void HUDRootPresenter_PublicSurface_RemainsBoundedToMappedFanOut()
        {
            var publicPropertyNames = typeof(HUDRootPresenter)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();
            Assert.That(publicPropertyNames, Is.EqualTo(new[] { "ViewModel" }));

            var publicMethodNames = typeof(HUDRootPresenter)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .OrderBy(name => name)
                .ToArray();
            Assert.That(publicMethodNames, Is.EqualTo(new[] { "Dispose" }));

            var constructors = typeof(HUDRootPresenter).GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            Assert.That(constructors, Has.Length.EqualTo(1));
            Assert.That(
                constructors[0].GetParameters().Select(parameter => parameter.ParameterType).ToArray(),
                Is.EqualTo(new[]
                {
                    typeof(IGameplayUiPresentationSource),
                    typeof(PlayerStatusPresenter),
                    typeof(ActionBarPresenter),
                    typeof(NotificationPresenter),
                }));

            var forbiddenTypes = new[]
            {
                typeof(IGameplayCommandGateway),
                typeof(IGameplayQueryFacade),
                typeof(ScreenController),
                typeof(PopupController),
                typeof(UIFlowCoordinator),
                typeof(PlayerStatusViewModel),
                typeof(ActionBarViewModel),
                typeof(NotificationViewModel),
            };

            foreach (var forbiddenType in forbiddenTypes)
            {
                Assert.That(
                    TypeDependsOn(typeof(HUDRootPresenter), forbiddenType),
                    Is.False,
                    $"{typeof(HUDRootPresenter).FullName} depends on {forbiddenType.FullName}");
            }
        }

        [Test]
        public void HUDRootPresenter_DoesNotExposeActionCommandEntryPoints()
        {
            var methodNames = typeof(HUDRootPresenter)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .ToArray();

            Assert.That(methodNames, Does.Not.Contain("RequestSlot"));
            Assert.That(methodNames, Does.Not.Contain("RequestMoveUp"));
            Assert.That(methodNames, Does.Not.Contain("RequestFlipRight"));
        }

        [Test]
        public void HUDRootViewModel_PublicProperties_RemainShellOnly()
        {
            var propertyNames = typeof(HUDRootViewModel)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(propertyNames, Is.EqualTo(new[] { "IsDimmed", "IsPauseButtonEnabled", "IsVisible" }));
        }

        [Test]
        public void HUDController_DoesNotDependOnMappedSnapshotOrFlowBlockContracts()
        {
            var forbiddenTypes = new[]
            {
                typeof(UIPresentationSnapshot),
                typeof(IGameplayUiPresentationSource),
                typeof(IGameplayPresentationFeed),
                typeof(UIBlockSnapshot),
            };

            foreach (var forbiddenType in forbiddenTypes)
            {
                Assert.That(
                    TypeDependsOn(typeof(HUDController), forbiddenType),
                    Is.False,
                    $"{typeof(HUDController).FullName} depends on {forbiddenType.FullName}");
            }

            Assert.That(
                typeof(HUDController).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Select(method => method.Name),
                Does.Not.Contain("ApplyBlockSnapshot"));
        }

        [Test]
        public void HUDController_PublicSurface_RemainsLifecycleAndBindingOnly()
        {
            var propertyNames = typeof(HUDController)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();
            Assert.That(
                propertyNames,
                Is.EqualTo(new[]
                {
                    "ActionBarViewModel",
                    "NotificationViewModel",
                    "PlayerStatusViewModel",
                    "RootViewModel",
                }));

            var methodNames = typeof(HUDController)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .OrderBy(name => name)
                .ToArray();
            Assert.That(methodNames, Is.EqualTo(new[] { "AttachView", "Dispose" }));
        }

        [Test]
        public void HudPresenters_DoNotDependOnFlowPolicyTypes()
        {
            var presenterTypes = new[]
            {
                typeof(HUDRootPresenter),
                typeof(PlayerStatusPresenter),
                typeof(ActionBarPresenter),
                typeof(NotificationPresenter),
            };
            var forbiddenTypes = new[]
            {
                typeof(UIFlowCoordinator),
                typeof(UIBlockPolicy),
                typeof(ScreenController),
                typeof(PopupController),
            };

            foreach (var presenterType in presenterTypes)
            {
                foreach (var forbiddenType in forbiddenTypes)
                {
                    Assert.That(
                        TypeDependsOn(presenterType, forbiddenType),
                        Is.False,
                        $"{presenterType.FullName} depends on {forbiddenType.FullName}");
                }
            }
        }

        [Test]
        public void HudViews_BindOnlyLocalViewModels()
        {
            AssertViewBindSignature(typeof(HUDRootView), typeof(HUDRootViewModel));
            AssertViewBindSignature(typeof(PlayerStatusView), typeof(PlayerStatusViewModel));
            AssertViewBindSignature(typeof(ActionBarView), typeof(ActionBarViewModel));
            AssertViewBindSignature(typeof(NotificationView), typeof(NotificationViewModel));
        }

        [Test]
        public void PopupViews_BindOnlyLocalPopupViewModels()
        {
            AssertViewBindSignature(typeof(PausePopupView), typeof(PausePopupViewModel));
            AssertViewBindSignature(typeof(ObjectiveInfoPopupView), typeof(ObjectiveInfoPopupViewModel));
            AssertViewBindSignature(typeof(ConfirmPopupView), typeof(ConfirmPopupViewModel));
            AssertViewBindSignature(typeof(TooltipPopupView), typeof(TooltipPopupViewModel));
            AssertViewBindSignature(typeof(RewardPopupView), typeof(RewardPopupViewModel));
            Assert.That(TypeDependsOn(typeof(PopupLayerView), typeof(UIPresentationSnapshot)), Is.False);
        }

        [Test]
        public void PopupPresenters_DoNotDependOnFlowOrRawGameplayPresentationTypes()
        {
            var presenterTypes = new[]
            {
                typeof(PausePopupPresenter),
                typeof(ObjectiveInfoPopupPresenter),
                typeof(ConfirmPopupPresenter),
                typeof(TooltipPopupPresenter),
                typeof(RewardPopupPresenter),
            };
            var forbiddenTypes = new[]
            {
                typeof(UIFlowCoordinator),
                typeof(PopupController),
                typeof(UIBlockPolicy),
                typeof(IGameplayPresentationFeed),
                typeof(GameplayPresentationFrame),
                typeof(GameplayPresentationState),
            };

            foreach (var presenterType in presenterTypes)
            {
                foreach (var forbiddenType in forbiddenTypes)
                {
                    Assert.That(
                        TypeDependsOn(presenterType, forbiddenType),
                        Is.False,
                        $"{presenterType.FullName} depends on {forbiddenType.FullName}");
                }
            }
        }

        [Test]
        public void InventoryPresenters_DoNotDependOnFlowPopupOrGameplayAuthorityTypes()
        {
            var presenterTypes = new[]
            {
                typeof(InventoryScreenPresenter),
                typeof(InventoryCatalogPresenter),
                typeof(InventoryDetailPresenter),
                typeof(InventoryActionPresenter),
            };
            var forbiddenTypes = new[]
            {
                typeof(UIFlowCoordinator),
                typeof(ScreenController),
                typeof(PopupController),
                typeof(PopupRequest),
                typeof(PopupId),
                typeof(IGameplayQueryFacade),
                typeof(IGameplayCommandGateway),
            };

            foreach (var presenterType in presenterTypes)
            {
                foreach (var forbiddenType in forbiddenTypes)
                {
                    Assert.That(
                        TypeDependsOn(presenterType, forbiddenType),
                        Is.False,
                        $"{presenterType.FullName} depends on {forbiddenType.FullName}");
                }
            }
        }

        [Test]
        public void InventoryChildPresenters_DoNotFormSiblingMeshes()
        {
            Assert.That(TypeDependsOn(typeof(InventoryCatalogPresenter), typeof(InventoryDetailPresenter)), Is.False);
            Assert.That(TypeDependsOn(typeof(InventoryCatalogPresenter), typeof(InventoryActionPresenter)), Is.False);
            Assert.That(TypeDependsOn(typeof(InventoryDetailPresenter), typeof(InventoryCatalogPresenter)), Is.False);
            Assert.That(TypeDependsOn(typeof(InventoryDetailPresenter), typeof(InventoryActionPresenter)), Is.False);
            Assert.That(TypeDependsOn(typeof(InventoryActionPresenter), typeof(InventoryCatalogPresenter)), Is.False);
            Assert.That(TypeDependsOn(typeof(InventoryActionPresenter), typeof(InventoryDetailPresenter)), Is.False);
        }

        [Test]
        public void UIFlowCoordinator_DoesNotDependOnPopupPresentersOrViewModels()
        {
            var forbiddenTypes = new[]
            {
                typeof(PausePopupPresenter),
                typeof(ObjectiveInfoPopupPresenter),
                typeof(ConfirmPopupPresenter),
                typeof(TooltipPopupPresenter),
                typeof(RewardPopupPresenter),
                typeof(PausePopupViewModel),
                typeof(ObjectiveInfoPopupViewModel),
                typeof(ConfirmPopupViewModel),
                typeof(TooltipPopupViewModel),
                typeof(RewardPopupViewModel),
                typeof(PausePopupView),
                typeof(ObjectiveInfoPopupView),
                typeof(ConfirmPopupView),
                typeof(TooltipPopupView),
                typeof(RewardPopupView),
            };

            foreach (var forbiddenType in forbiddenTypes)
            {
                Assert.That(
                    TypeDependsOn(typeof(UIFlowCoordinator), forbiddenType),
                    Is.False,
                    $"{typeof(UIFlowCoordinator).FullName} depends on {forbiddenType.FullName}");
            }
        }

        [Test]
        public void UIBlockSnapshot_PublicSurface_RemainsPolicyOnly()
        {
            var propertyNames = typeof(UIBlockSnapshot)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(
                propertyNames,
                Is.EqualTo(new[]
                {
                    "BlocksHudInteraction",
                    "BlocksLowerLayerPointer",
                    "BlocksScreenInteraction",
                    "BlocksUiGameplayInput",
                    "PopupBackdropMode",
                    "PopupConsumesBack",
                    "ShowsPopupDim",
                }));
        }

        private static Assembly[] GetRuntimeUiAssemblies()
        {
            return new[]
            {
                typeof(HUDRootPresenter).Assembly,
                typeof(HUDController).Assembly,
                typeof(HUDRootView).Assembly,
                typeof(GameplayScreenView).Assembly,
                typeof(PausePopupView).Assembly,
                typeof(GameplayUiFlowInstaller).Assembly,
            }.Distinct().ToArray();
        }

        private static IEnumerable<Type> GetPublicSurfaceTypes(Type type)
        {
            yield return type;

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                foreach (var surfacedType in ExpandType(property.PropertyType))
                {
                    yield return surfacedType;
                }
            }

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName)
                {
                    continue;
                }

                foreach (var surfacedType in ExpandType(method.ReturnType))
                {
                    yield return surfacedType;
                }

                foreach (var parameter in method.GetParameters())
                {
                    foreach (var surfacedType in ExpandType(parameter.ParameterType))
                    {
                        yield return surfacedType;
                    }
                }
            }
        }

        private static void AssertViewBindSignature(Type viewType, Type expectedViewModelType)
        {
            var bindMethods = viewType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.Name == "Bind")
                .ToArray();

            Assert.That(bindMethods, Has.Length.EqualTo(1), viewType.FullName);
            Assert.That(bindMethods[0].GetParameters().Select(parameter => parameter.ParameterType).ToArray(), Is.EqualTo(new[] { expectedViewModelType }));
            Assert.That(TypeDependsOn(viewType, typeof(UIPresentationSnapshot)), Is.False, viewType.FullName);
        }

        private static IEnumerable<Type> ExpandType(Type type)
        {
            var normalizedType = NormalizeType(type);
            if (normalizedType == null)
            {
                yield break;
            }

            yield return normalizedType;

            if (!normalizedType.IsGenericType)
            {
                yield break;
            }

            foreach (var argument in normalizedType.GetGenericArguments())
            {
                foreach (var surfacedType in ExpandType(argument))
                {
                    yield return surfacedType;
                }
            }
        }

        private static Type NormalizeType(Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (type.IsByRef)
            {
                type = type.GetElementType();
            }

            if (type.IsArray)
            {
                type = type.GetElementType();
            }

            return Nullable.GetUnderlyingType(type) ?? type;
        }

        private static bool TypeDependsOn(Type type, Type dependencyType)
        {
            if (type == null || dependencyType == null)
            {
                return false;
            }

            return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                       .Any(field => NormalizeType(field.FieldType) == dependencyType) ||
                   type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                       .Any(property => NormalizeType(property.PropertyType) == dependencyType) ||
                   type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       .Any(ctor => ctor.GetParameters().Any(parameter => NormalizeType(parameter.ParameterType) == dependencyType)) ||
                   type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                       .Any(method =>
                           NormalizeType(method.ReturnType) == dependencyType ||
                           method.GetParameters().Any(parameter => NormalizeType(parameter.ParameterType) == dependencyType));
        }
    }
}
