using System.Drawing;
using SkrinshotUZ.Core.Editing;
using SkrinshotUZ.Editing;

namespace SkrinshotUZ.Windows.Tests.Editing;

/// <summary>Ikonkalar va panellar.</summary>
public class ToolbarTests
{
    [Theory]
    [InlineData(24)]
    [InlineData(30)]
    [InlineData(36)]
    [InlineData(48)]
    public void EveryIcon_HasVisiblePixels_AtEveryDpiSize(int size)
    {
        foreach (var icon in Enum.GetValues<ToolbarIcon>())
        {
            using var bmp = IconPainter.Draw(icon, size);
            Assert.Equal(new Size(size, size), bmp.Size);

            int visible = 0;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (bmp.GetPixel(x, y).A > 128)
                    {
                        visible++;
                    }
                }
            }

            // Ikonka bo'sh emas va butun maydonni bo'yab ham qo'ymagan.
            Assert.True(visible > size, $"{icon} ({size}px): ko'rinadigan piksel juda kam — {visible}");
            Assert.True(visible < size * size * 0.8, $"{icon} ({size}px): juda ko'p — {visible}");
        }
    }

    [Fact]
    public void Swatch_HasChosenColorInCenter()
    {
        using var bmp = IconPainter.Swatch(Color.FromArgb(30, 160, 60), 24);
        var c = bmp.GetPixel(12, 12);

        Assert.Equal(Color.FromArgb(255, 30, 160, 60), c);
        Assert.Equal(0, bmp.GetPixel(0, 0).A); // burchak shaffof
    }

    [Fact]
    public void Toolbars_HaveTooltipsForEveryButton_AndScaleWithDpi()
    {
        using var bars = new EditorToolbars();
        bars.SetScale(1.0);
        var size100 = bars.Tools.PreferredSize;
        bars.SetScale(2.0);
        var size200 = bars.Tools.PreferredSize;

        foreach (ToolStripItem item in bars.Tools.Items)
        {
            if (item is not ToolStripSeparator)
            {
                Assert.False(string.IsNullOrWhiteSpace(item.ToolTipText), "har bir tugmada o'zbekcha tooltip bo'lishi kerak");
                Assert.NotNull(item.Image);
            }
        }

        Assert.True(size200.Height > size100.Height * 1.6, $"200% da panel kattaroq bo'lishi kerak: {size100} → {size200}");
    }

    [Fact]
    public void Toolbars_RaiseEvents()
    {
        using var bars = new EditorToolbars();
        bars.SetScale(1.0);
        EditorTool? chosen = null;
        bars.ToolClicked += (_, t) => chosen = t;

        var arrowButton = bars.Tools.Items.OfType<ToolStripButton>().First(b => b.ToolTipText == EditorUi.ToolName(EditorTool.Arrow));
        arrowButton.PerformClick();

        Assert.Equal(EditorTool.Arrow, chosen);
    }

    [Fact]
    public void Toolbars_UpdateState_ChecksActiveTool_DisablesUndoWhenEmpty()
    {
        using var bars = new EditorToolbars();
        bars.SetScale(1.0);
        bars.UpdateState(EditorTool.Rectangle, Color.Blue, Thickness.Thick, canUndo: false, canRedo: true);

        var buttons = bars.Tools.Items.OfType<ToolStripButton>().ToList();
        Assert.True(buttons.Single(b => b.ToolTipText == EditorUi.ToolName(EditorTool.Rectangle)).Checked);
        Assert.False(buttons.Single(b => b.ToolTipText == EditorUi.ToolName(EditorTool.Pen)).Checked);
        Assert.False(buttons.Single(b => b.ToolTipText?.StartsWith("Bekor", StringComparison.Ordinal) == true).Enabled);
        Assert.True(buttons.Single(b => b.ToolTipText?.StartsWith("Qaytarish", StringComparison.Ordinal) == true).Enabled);
    }
}
