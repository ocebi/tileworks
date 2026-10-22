using NUnit.Framework;
using Tileworks.Tiles.View;
using UnityEngine;

namespace Tileworks.Tiles.Tests
{
    public sealed class GridWindowTests
    {
        // The level list's well: 4 columns of 160 × 160 cells, 30 × 35 apart, 30 above and below.
        private static GridWindow Well() => new GridWindow(4, new Vector2(160, 160), new Vector2(30, 35), new RectOffset(0, 0, 30, 30));

        [Test]
        public void ContentHeight_MatchesTheGridLayout()
        {
            Assert.That(Well().ContentHeight(1), Is.EqualTo(30 + 160 + 30));
            Assert.That(Well().ContentHeight(8), Is.EqualTo(30 + 160 + 35 + 160 + 30));
            Assert.That(Well().ContentHeight(0), Is.EqualTo(60));
        }

        [Test]
        public void PositionOf_CentresEachRow()
        {
            var layout = Well();
            Assert.That(layout.PositionOf(0, 730), Is.EqualTo(new Vector2(80, -110)));
            Assert.That(layout.PositionOf(3, 730), Is.EqualTo(new Vector2(650, -110)));
            Assert.That(layout.PositionOf(5, 730), Is.EqualTo(new Vector2(270, -305)));
            Assert.That(layout.PositionOf(0, 760), Is.EqualTo(new Vector2(95, -110)), "a wider well keeps the row centred");
        }

        [Test]
        public void Visible_CoversTheViewportAndOneRowEitherSide()
        {
            var layout = Well();
            Assert.That(layout.Visible(1000, 0f, 860f, 1), Is.EqualTo((0, 24)));
            var (first, end) = layout.Visible(1000, 195f * 100, 860f, 1);
            Assert.That(first, Is.EqualTo(98 * 4));
            Assert.That(end, Is.EqualTo(106 * 4));
            Assert.That(layout.Visible(10, 0f, 860f, 1), Is.EqualTo((0, 10)));
        }

        [Test]
        public void CellsFor_IsEnoughAtEveryScrollOffset()
        {
            var layout = Well();
            var cells = layout.CellsFor(860f, 1);
            for (var y = 0f; y < 200000f; y += 37f)
            {
                var (first, end) = layout.Visible(5000, y, 860f, 1);
                Assert.That(end - first, Is.LessThanOrEqualTo(cells), $"scroll {y}");
            }
        }

        [Test]
        public void ScrollToCentre_StaysInRange()
        {
            var layout = Well();
            Assert.That(layout.ScrollToCentre(0, 1000, 860f), Is.EqualTo(0f));
            Assert.That(layout.ScrollToCentre(999, 1000, 860f), Is.EqualTo(layout.ContentHeight(1000) - 860f));
            Assert.That(layout.ScrollToCentre(400, 1000, 860f), Is.EqualTo(30 + 100 * 195 + 80 - 430f));
        }
    }
}
