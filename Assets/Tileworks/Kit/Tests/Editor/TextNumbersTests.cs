using NUnit.Framework;
using Tileworks.Kit.UI;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Tileworks.Kit.Tests
{
    public sealed class TextNumbersTests
    {
        [Test]
        public void SmallNumbers_ShareOneString()
        {
            Assert.That(TextNumbers.Of(0), Is.EqualTo("0"));
            Assert.That(ReferenceEquals(TextNumbers.Of(999), TextNumbers.Of(999)), NUnit.Framework.Is.True);
        }

        [Test]
        public void Append_FormatsPrefixesPaddingAndGrouping()
        {
            var text = new TextNumbers();
            Assert.That(text.Clear().Append("Level ").Append(12).ToString(), Is.EqualTo("Level 12"));
            Assert.That(text.Clear().Append(5, minDigits: 2).Append(':').Append(9, minDigits: 2).ToString(), Is.EqualTo("05:09"));
            Assert.That(text.Clear().Append(1234567, grouped: true).ToString(), Is.EqualTo("1,234,567"));
            Assert.That(text.Clear().Append(1000, grouped: true).ToString(), Is.EqualTo("1,000"));
            Assert.That(text.Clear().Append(0).ToString(), Is.EqualTo("0"));
            Assert.That(text.Clear().Append(-42).ToString(), Is.EqualTo("-42"));
        }

        [Test]
        public void Append_DoesNotAllocateAfterWarmUp()
        {
            var text = new TextNumbers();
            TestDelegate format = () => text.Clear().Append("Level ").Append(1234567, grouped: true).Append(" of ").Append(7, minDigits: 2);
            format();
            Assert.That(format, Is.Not.AllocatingGCMemory());
        }
    }
}
