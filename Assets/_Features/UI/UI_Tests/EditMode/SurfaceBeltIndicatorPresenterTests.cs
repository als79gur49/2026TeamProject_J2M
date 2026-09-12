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
                currentSlotIndex: SurfaceBeltSlotMapping.SurfaceCount - 1,
                sourceSlotIndex: SurfaceBeltSlotMapping.SurfaceCount - 1,
                destinationSlotIndex: SurfaceBeltSlotMapping.SurfaceCount - 1,
                SurfaceBeltDirection.None,
                isTransitioning: false,
                transitionSequenceId: 0));

            var firstOffset = -(SurfaceBeltViewModel.AuthoredCellCount / 2);
            var expectedOffsets = Enumerable.Range(firstOffset, SurfaceBeltViewModel.AuthoredCellCount).ToArray();
            Assert.That(presenter.ViewModel.Cells, Has.Length.EqualTo(SurfaceBeltViewModel.AuthoredCellCount));
            Assert.That(presenter.ViewModel.Cells.Select(cell => cell.Offset).ToArray(), Is.EqualTo(expectedOffsets));
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

        [Test]
        public void Apply_PreservesButtonRemaindersBySlot()
        {
            var presenter = new SurfaceBeltIndicatorPresenter();

            presenter.Apply(new SurfaceBeltSnapshot(
                currentSlotIndex: 0,
                sourceSlotIndex: 0,
                destinationSlotIndex: 0,
                SurfaceBeltDirection.None,
                isTransitioning: false,
                transitionSequenceId: 0,
                new[]
                {
                    new SurfaceBeltButtonRemainderSnapshot(0, 1, 0),
                    new SurfaceBeltButtonRemainderSnapshot(1, 0, 2),
                    new SurfaceBeltButtonRemainderSnapshot(2, 3, 4),
                    new SurfaceBeltButtonRemainderSnapshot(3, 0, 0),
                }));

            Assert.That(presenter.ViewModel.ButtonRemainders, Has.Length.EqualTo(SurfaceBeltSlotMapping.SurfaceCount));
            Assert.That(presenter.ViewModel.ButtonRemainders[0].NormalRemaining, Is.EqualTo(1));
            Assert.That(presenter.ViewModel.ButtonRemainders[1].MoonBlockOnlyRemaining, Is.EqualTo(2));
            Assert.That(presenter.ViewModel.ButtonRemainders[2].NormalRemaining, Is.EqualTo(3));
            Assert.That(presenter.ViewModel.ButtonRemainders[2].MoonBlockOnlyRemaining, Is.EqualTo(4));
        }

        [Test]
        public void SetState_RaisesChangedOnlyWhenButtonRemaindersChange()
        {
            var viewModel = new SurfaceBeltViewModel();
            var changeCount = 0;
            viewModel.Changed += () => changeCount++;
            var cells = SurfaceBeltSlotMapping.BuildCells(0);
            var firstRemainders = new[]
            {
                new SurfaceBeltButtonRemainderViewModel(0, 1, 0),
                new SurfaceBeltButtonRemainderViewModel(1, 0, 0),
                new SurfaceBeltButtonRemainderViewModel(2, 0, 0),
                new SurfaceBeltButtonRemainderViewModel(3, 0, 0),
            };
            var secondRemainders = new[]
            {
                new SurfaceBeltButtonRemainderViewModel(0, 1, 0),
                new SurfaceBeltButtonRemainderViewModel(1, 0, 2),
                new SurfaceBeltButtonRemainderViewModel(2, 0, 0),
                new SurfaceBeltButtonRemainderViewModel(3, 0, 0),
            };

            viewModel.SetState(true, 0, 0, 0, SurfaceBeltDirection.None, false, 0, cells, firstRemainders);
            viewModel.SetState(true, 0, 0, 0, SurfaceBeltDirection.None, false, 0, cells, firstRemainders);
            viewModel.SetState(true, 0, 0, 0, SurfaceBeltDirection.None, false, 0, cells, secondRemainders);

            Assert.That(changeCount, Is.EqualTo(2));
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
