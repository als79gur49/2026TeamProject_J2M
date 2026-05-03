using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageAuthoringKindRegistryTests
    {
        [TestCase(StageAuthoringEntityKind.Player, "P", "Player", StageAuthoringPresentationLane.None, false, false, true, true, false)]
        [TestCase(StageAuthoringEntityKind.Enemy, "E", "Enemy", StageAuthoringPresentationLane.Enemy, true, true, true, true, false)]
        [TestCase(StageAuthoringEntityKind.Box, "B", "Box", StageAuthoringPresentationLane.Static, true, true, true, false, true)]
        [TestCase(StageAuthoringEntityKind.Wall, "W", "Wall", StageAuthoringPresentationLane.Static, true, true, true, false, true)]
        public void Registry_ReturnsExpectedDescriptor(
            StageAuthoringEntityKind kind,
            string marker,
            string displayName,
            StageAuthoringPresentationLane presentationLane,
            bool requiresPresentation,
            bool requiresPresentationBinding,
            bool supportsFacingAuthoring,
            bool isUnitLike,
            bool isStaticLike)
        {
            var descriptor = StageAuthoringKindRegistry.Get(kind);

            Assert.That(descriptor.Kind, Is.EqualTo(kind));
            Assert.That(descriptor.Marker, Is.EqualTo(marker));
            Assert.That(descriptor.DisplayName, Is.EqualTo(displayName));
            Assert.That(descriptor.PresentationLane, Is.EqualTo(presentationLane));
            Assert.That(descriptor.RequiresPresentation, Is.EqualTo(requiresPresentation));
            Assert.That(descriptor.RequiresPresentationBinding, Is.EqualTo(requiresPresentationBinding));
            Assert.That(descriptor.SupportsFacingAuthoring, Is.EqualTo(supportsFacingAuthoring));
            Assert.That(descriptor.IsUnitLike, Is.EqualTo(isUnitLike));
            Assert.That(descriptor.IsStaticLike, Is.EqualTo(isStaticLike));
        }

        [Test]
        public void Registry_DescriptorsExposeExistingKindsOnlyInStableOrder()
        {
            var descriptors = StageAuthoringKindRegistry.Descriptors;

            Assert.That(descriptors.Count, Is.EqualTo(4));
            Assert.That(descriptors[0].Kind, Is.EqualTo(StageAuthoringEntityKind.Player));
            Assert.That(descriptors[1].Kind, Is.EqualTo(StageAuthoringEntityKind.Enemy));
            Assert.That(descriptors[2].Kind, Is.EqualTo(StageAuthoringEntityKind.Box));
            Assert.That(descriptors[3].Kind, Is.EqualTo(StageAuthoringEntityKind.Wall));
        }

        [Test]
        public void Registry_TryGetUnknownKind_ReturnsFalse()
        {
            Assert.That(StageAuthoringKindRegistry.TryGet((StageAuthoringEntityKind)99, out var descriptor), Is.False);
            Assert.That(descriptor, Is.EqualTo(default(StageAuthoringKindDescriptor)));
            Assert.That(StageAuthoringKindRegistry.TryGetMarker((StageAuthoringEntityKind)99, out var marker), Is.False);
            Assert.That(marker, Is.EqualTo(string.Empty));
            Assert.That(StageAuthoringKindRegistry.GetPresentationLane((StageAuthoringEntityKind)99), Is.EqualTo(StageAuthoringPresentationLane.None));
        }

        [Test]
        public void Registry_GetUnknownKind_Throws()
        {
            Assert.That(
                () => StageAuthoringKindRegistry.Get((StageAuthoringEntityKind)99),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }
}
