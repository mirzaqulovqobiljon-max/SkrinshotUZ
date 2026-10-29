using SkrinshotUZ.Core.Startup;

namespace SkrinshotUZ.Windows.Tests.App;

/// <summary>Dastur yopilishi.</summary>
public class TrayContextTests
{
    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        // Windows Forms kontekstni yopilishda ikki marta tozalaydi (Application.Run tugaganda va using'da).
        // 1.1.0 ishlanmasida ikkinchi marta CancellationTokenSource.Cancel() xato berib, yopilishda xatolik oynasi chiqardi.
        using var activate = new EventWaitHandle(false, EventResetMode.AutoReset);
        using var exit = new EventWaitHandle(false, EventResetMode.ManualReset);
        var context = new TrayApplicationContext(StartupOptions.Default, activate, exit);

        context.Dispose();
        var second = Record.Exception(context.Dispose);

        Assert.Null(second);
    }
}
