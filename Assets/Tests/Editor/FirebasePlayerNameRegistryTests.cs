using NUnit.Framework;

namespace NebulaSoft.Tests
{
    public class FirebasePlayerNameRegistryTests
    {
        [Test]
        public void TryNormalize_TrimsAndCollapsesWhitespace()
        {
            bool valid = FirebasePlayerNameRegistry.TryNormalize(
                "  Cute   Player  ",
                out string displayName,
                out string normalizedName);

            Assert.That(valid, Is.True);
            Assert.That(displayName, Is.EqualTo("Cute Player"));
            Assert.That(normalizedName, Is.EqualTo("cute player"));
            Assert.That(FirebasePlayerNameRegistry.ComputeKey(normalizedName), Has.Length.EqualTo(64));
        }

        [TestCase("ab")]
        [TestCase("this player name is longer than sixteen")]
        public void TryNormalize_RejectsNamesOutsideTheConfiguredLength(string value)
        {
            Assert.That(FirebasePlayerNameRegistry.TryNormalize(value, out _, out _), Is.False);
        }

        [TestCase("", "Enter a player name.")]
        [TestCase("ab", "Name must contain at least 3 characters.")]
        [TestCase("this player name is longer than sixteen", "Name can contain at most 16 characters.")]
        [TestCase("na\nme", "Name contains unsupported characters.")]
        public void GetValidationError_ReturnsPlayerFacingReason(string value, string expectedMessage)
        {
            Assert.That(FirebasePlayerNameRegistry.GetValidationError(value), Is.EqualTo(expectedMessage));
        }

        [Test]
        public void CreateDefaultName_AlwaysFitsPlayerNameRules()
        {
            string name = FirebasePlayerNameRegistry.CreateDefaultName("01234567-89ab-cdef-0123-456789abcdef", 4);

            Assert.That(FirebasePlayerNameRegistry.TryNormalize(name, out string displayName, out _), Is.True);
            Assert.That(displayName, Is.EqualTo(name));
        }
    }
}
