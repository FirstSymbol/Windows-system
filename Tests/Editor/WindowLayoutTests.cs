using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WindowsSystem.Layouts;

namespace WindowsSystem.Tests
{
  public sealed class WindowLayoutTests
  {
    private static readonly Rect Area = new(0, 0, 1920, 1080);

    private static void AssertRect(Rect expected, Rect actual)
    {
      Assert.AreEqual(expected.x, actual.x, 0.01f, "x");
      Assert.AreEqual(expected.y, actual.y, 0.01f, "y");
      Assert.AreEqual(expected.width, actual.width, 0.01f, "width");
      Assert.AreEqual(expected.height, actual.height, 0.01f, "height");
    }

    [Test]
    public void StretchSlotUsesAnchorsAndPadding()
    {
      WindowSlotRect slot = WindowSlotRect.Area(Vector2.zero, new Vector2(0.25f, 1f), padding: 10);
      AssertRect(new Rect(10, 10, 460, 1060), WindowLayoutMath.ComputeSlot(slot, Area));
    }

    [Test]
    public void FixedSlotIsAlignedAndClamped()
    {
      WindowSlotRect centered = WindowSlotRect.Sized(Vector2.zero, Vector2.one, WindowSlotSizeMode.Fixed,
        new Vector2(800, 600), new Vector2(0.5f, 0.5f));
      AssertRect(new Rect(560, 240, 800, 600), WindowLayoutMath.ComputeSlot(centered, Area));

      WindowSlotRect huge = WindowSlotRect.Sized(Vector2.zero, Vector2.one, WindowSlotSizeMode.Fixed,
        new Vector2(4000, 4000), new Vector2(0.5f, 1f));
      AssertRect(new Rect(0, 0, 1920, 1080), WindowLayoutMath.ComputeSlot(huge, Area));
    }

    [Test]
    public void FitAspectKeepsProportions()
    {
      WindowSlotRect slot = WindowSlotRect.Sized(Vector2.zero, Vector2.one, WindowSlotSizeMode.FitAspect,
        new Vector2(1000, 1000), new Vector2(0.5f, 0.5f));
      Rect rect = WindowLayoutMath.ComputeSlot(slot, Area);
      Assert.AreEqual(1f, rect.width / rect.height, 0.001f);
      Assert.AreEqual(1000f, rect.width, 0.01f, "Limited by the size.");

      slot.size = new Vector2(16, 9);
      rect = WindowLayoutMath.ComputeSlot(slot, new Rect(0, 0, 1080, 1920));
      Assert.AreEqual(16f / 9f, rect.width / rect.height, 0.001f);
      Assert.AreEqual(16f, rect.width, 0.01f, "Small sizes act as a limit too.");
    }

    [Test]
    public void PortraitRectIsUsedForTallRoots()
    {
      var slot = new WindowSlot("Panel", Color.white,
        WindowSlotRect.Area(Vector2.zero, new Vector2(0.3f, 1f)),
        WindowSlotRect.Area(Vector2.zero, new Vector2(1f, 0.5f)));

      Assert.AreEqual(0.3f, slot.Resolve(new Vector2(1920, 1080)).anchorMax.x);
      Assert.AreEqual(0.5f, slot.Resolve(new Vector2(1080, 1920)).anchorMax.y);
    }

    [TestCase(WindowTilingMode.Horizontal, 3)]
    [TestCase(WindowTilingMode.Vertical, 3)]
    [TestCase(WindowTilingMode.Grid, 5)]
    [TestCase(WindowTilingMode.MainAndStack, 4)]
    [TestCase(WindowTilingMode.Cascade, 4)]
    [TestCase(WindowTilingMode.Center, 2)]
    [TestCase(WindowTilingMode.Maximize, 2)]
    public void TilingProducesOneRectPerWindowInsideTheArea(WindowTilingMode mode, int count)
    {
      var rects = new List<Rect>();
      WindowLayoutMath.ComputeTiling(mode, count, Area, 16, rects);

      Assert.AreEqual(count, rects.Count);
      foreach (Rect rect in rects)
      {
        Assert.GreaterOrEqual(rect.xMin, Area.xMin - 0.01f);
        Assert.GreaterOrEqual(rect.yMin, Area.yMin - 0.01f);
        Assert.LessOrEqual(rect.xMax, Area.xMax + 0.01f);
        Assert.LessOrEqual(rect.yMax, Area.yMax + 0.01f);
        Assert.Greater(rect.width, 0f);
        Assert.Greater(rect.height, 0f);
      }
    }

    [Test]
    public void HorizontalTilingDoesNotOverlapAndKeepsSpacing()
    {
      var rects = new List<Rect>();
      WindowLayoutMath.ComputeTiling(WindowTilingMode.Horizontal, 3, Area, 30, rects);
      Assert.AreEqual(620f, rects[0].width, 0.01f);
      Assert.AreEqual(rects[0].xMax + 30f, rects[1].xMin, 0.01f);
      Assert.AreEqual(Area.xMax, rects[2].xMax, 0.01f);
    }

    [Test]
    public void GridAdaptsToAreaProportions()
    {
      var rects = new List<Rect>();
      WindowLayoutMath.ComputeTiling(WindowTilingMode.Grid, 4, Area, 0, rects);
      Assert.AreEqual(2, rects.Select(r => r.x).Distinct().Count(), "2 columns for 4 windows on 16:9");

      WindowLayoutMath.ComputeTiling(WindowTilingMode.Grid, 3, new Rect(0, 0, 1080, 1920), 0, rects);
      Assert.AreEqual(1, rects.Select(r => r.x).Distinct().Count(), "a single column on a tall area");
      Assert.Greater(rects[0].y, rects[1].y, "rows go from top to bottom");
    }

    [Test]
    public void MainAndStackGivesTheFirstWindowTheMostSpace()
    {
      var rects = new List<Rect>();
      WindowLayoutMath.ComputeTiling(WindowTilingMode.MainAndStack, 3, Area, 0, rects);
      Assert.Greater(rects[0].width * rects[0].height, rects[1].width * rects[1].height * 2f);
      Assert.AreEqual(rects[1].x, rects[2].x, 0.01f);
    }

    [Test]
    public void LibraryFindsSlotsByIdAndName()
    {
      var library = ScriptableObject.CreateInstance<WindowLayoutLibrary>();
      library.ResetToDefaults();
      WindowSlot popup = library.Find("center popup");
      Assert.IsNotNull(popup);
      Assert.AreSame(popup, library.Find(popup.Id));
      Assert.AreSame(popup, library.Find(new WindowSlotRef("missing-id", "Center Popup")), "Falls back to the name.");
      Object.DestroyImmediate(library);
    }
  }
}
