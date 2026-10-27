using SkrinshotUZ.Core.Localization;
using SkrinshotUZ.Core.Permissions;

namespace SkrinshotUZ.UI;

/// <summary>
/// Savol oynalari — Windows'ning standart TaskDialog'i: har qanday DPI'da to'g'ri chiziladi,
/// katta "buyruq havolasi" tugmalarida qisqa izoh bor (birinchi marta ko'rgan o'qituvchi ham tushunadi).
/// </summary>
internal sealed class WindowsUserPrompts : IUserPrompts
{
    public PrintScreenAnswer AskPrintScreenPermission(bool offerAutostart)
    {
        using var icon = AppIcon.LoadLarge();
        var allow = new TaskDialogCommandLinkButton(Strings.Permission_Allow, Strings.Permission_Allow_Note);
        var deny = new TaskDialogCommandLinkButton(Strings.Permission_Deny, Strings.Permission_Deny_Note);
        var page = new TaskDialogPage
        {
            Caption = Strings.Permission_Title,
            Heading = Strings.Permission_PrintScreen_Question,
            Text = Strings.Permission_Explanation,
            Icon = new TaskDialogIcon(icon),
            AllowCancel = true,
            SizeToContent = true,
        };
        page.Buttons.Add(allow);
        page.Buttons.Add(deny);
        page.DefaultButton = allow;

        // Avtoishga tushirish — alohida rozilik, standart holatda BELGILANMAGAN.
        if (offerAutostart)
        {
            page.Verification = new TaskDialogVerificationCheckBox(Strings.Permission_Autostart, false);
        }

        var result = TaskDialog.ShowDialog(page, TaskDialogStartupLocation.CenterScreen);

        var choice = ReferenceEquals(result, allow) ? PermissionChoice.Allow
            : ReferenceEquals(result, deny) ? PermissionChoice.Deny
            : PermissionChoice.Dismissed;
        bool autostart = offerAutostart && page.Verification is { Checked: true };
        return new PrintScreenAnswer(choice, autostart);
    }

    public bool AskDisableSnippingTool()
    {
        var disable = new TaskDialogCommandLinkButton(Strings.Snipping_Disable, Strings.Snipping_Disable_Note);
        var keep = new TaskDialogCommandLinkButton(Strings.Snipping_Keep, Strings.Snipping_Keep_Note);
        var page = new TaskDialogPage
        {
            Caption = Strings.Snipping_Title,
            Heading = Strings.Snipping_Heading,
            Text = Strings.Snipping_Text,
            Icon = TaskDialogIcon.Information,
            AllowCancel = true,
            SizeToContent = true,
        };
        page.Buttons.Add(disable);
        page.Buttons.Add(keep);
        page.DefaultButton = keep; // tizim sozlamasini o'zgartirish — standart tanlov emas

        return ReferenceEquals(TaskDialog.ShowDialog(page, TaskDialogStartupLocation.CenterScreen), disable);
    }

    public bool AskRestoreSnippingTool()
    {
        var yes = new TaskDialogButton(Strings.Snipping_Restore_Yes);
        var no = new TaskDialogButton(Strings.Snipping_Restore_No);
        var page = new TaskDialogPage
        {
            Caption = Strings.Snipping_Title,
            Text = Strings.Snipping_Restore_Question,
            Icon = TaskDialogIcon.Information,
            AllowCancel = true,
            SizeToContent = true,
        };
        page.Buttons.Add(yes);
        page.Buttons.Add(no);
        page.DefaultButton = no;

        return ReferenceEquals(TaskDialog.ShowDialog(page, TaskDialogStartupLocation.CenterScreen), yes);
    }

    public void ShowSnippingToolDisabled() =>
        ShowMessage(Strings.Snipping_Title, Strings.Snipping_Done, TaskDialogIcon.Information);

    public void ShowWarning(string message) =>
        ShowMessage(Strings.AppName, message, TaskDialogIcon.Warning);

    /// <summary>Oddiy ma'lumot oynasi (OK tugmasi bilan).</summary>
    public void ShowInfo(string title, string text) =>
        ShowMessage(title, text, TaskDialogIcon.Information);

    private static void ShowMessage(string caption, string text, TaskDialogIcon icon)
    {
        var page = new TaskDialogPage
        {
            Caption = caption,
            Text = text,
            Icon = icon,
            AllowCancel = true,
            SizeToContent = true,
        };
        page.Buttons.Add(TaskDialogButton.OK);
        TaskDialog.ShowDialog(page, TaskDialogStartupLocation.CenterScreen);
    }
}
