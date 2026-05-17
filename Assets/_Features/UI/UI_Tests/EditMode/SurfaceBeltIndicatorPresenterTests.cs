using System;
using System.Linq;
using System.Reflection;
using Game.Feature.UI.Application;
using Game.Feature.UI.HUD;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class SurfaceBeltIndicatorPresenterTests
    {
        [Test]
        public void Apply_CreatesSevenAuthoredCellsWithOnlyCenterCurrent()
        {
            var presenter = new SurfaceBeltIndicatorPresenter();

            presenter.Apply(new SurfaceBeltSnapshot(
                currentSlotIndex: 3,
                sourceSlotIndex: 3,
                destinationSlotIndex: 3,
                SurfaceBeltDirection.None,
                isTransitioning: false,
                transitionSequenceId: 0));

            Assert.That(presenter.ViewModel.Cells, Has.Length.EqualTo(7));
            Assert.That(presenter.ViewModel.Cells.Select(cell => cell.Offset).ToArray(), Is.EqualTo(new[] { -3, -2, -1, 0, 1, 2, 3 }));
            Assert.That(presenter.ViewModel.Cells.Count(cell => cell.IsCurrent), Is.EqualTo(1));
            Assert.That(presenter.ViewModel.Cells.Single(cell => cell.IsCurrent).Offset, Is.EqualTo(0));
        }

        [Test]
        public void Apply_DoesNotExposeFaceNameStringsInViewModelSurface()
        {
            var forbiddenStringMembers = typeof(SurfaceBeltViewModel)
                .GetMembers(BindingFlags.Instance | BindingFlags.Public)
                .Where(member => member.MemberType == MemberTypes.Property || member.MemberType == MemberTypes.Field)
                .Where(member => GetMemberType(member) == typeof(string))
                .Select(member => member.Name)
                .ToArray();

            Assert.That(forbiddenStringMembers, Is.Empty);
        }

        [Test]
        public void Apply_PreservesTransitionDirectionAndSequenceId()
        {
            var presenter = new SurfaceBeltIndicatorPresenter();

            presenter.Apply(new SurfaceBeltSnapshot(
                currentSlotIndex: 0,
                sourceSlotIndex: 3,
                destinationSlotIndex: 0,
                SurfaceBeltDirection.Forward,
                isTransitioning: true,
                transitionSequenceId: 3101));

            Assert.That(presenter.ViewModel.IsTransitioning, Is.True);
            Assert.That(presenter.ViewModel.Direction, Is.EqualTo(SurfaceBeltDirection.Forward));
            Assert.That(presenter.ViewModel.TransitionSequenceId, Is.EqualTo(3101));
            Assert.That(presenter.ViewModel.CenterSlotIndex, Is.EqualTo(3));
            Assert.That(presenter.ViewModel.DestinationSlotIndex, Is.EqualTo(0));
        }

        private static Type GetMemberType(MemberInfo member)
        {
            return member switch
            {
                PropertyInfo property => property.PropertyType,
                FieldInfo field => field.FieldType,
                _ => null,
            };
        }
    }
}
