using SkrinshotUZ.Core.Localization;

namespace SkrinshotUZ.UI;

/// <summary>
/// "Matn aniqlanmoqda…" oynasi: ish fon oqimida bajariladi, oyna esa javob berib turadi (Windows "javob bermayapti"
/// demaydi). Ish tugamaguncha foydalanuvchi oynani yopa olmaydi — yarim natija bo'lmasin.
/// </summary>
/// <typeparam name="T">Natija turi.</typeparam>
internal sealed class OcrProgressForm<T> : Form
    where T : class
{
    private readonly Func<T> _work;
    private readonly Font _font;
    private readonly Icon _icon;
    private bool _finished;

    public OcrProgressForm(Func<T> work)
    {
        _work = work ?? throw new ArgumentNullException(nameof(work));
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        _font = new Font("Segoe UI", 10.5f);
        _icon = AppIcon.LoadLarge();
        Font = _font;
        Icon = _icon;
        Text = Strings.Ocr_Title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ControlBox = false;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ClientSize = new Size(400, 120);
        BackColor = Color.White;

        var label = new Label
        {
            Text = Strings.Ocr_Progress,
            Dock = DockStyle.Top,
            Height = 56,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        var bar = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Dock = DockStyle.Top,
            Height = 22,
        };
        var pad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 12, 24, 16) };
        pad.Controls.Add(bar);
        pad.Controls.Add(label);
        Controls.Add(pad);
        ResumeLayout(false);

        Shown += async (_, _) =>
        {
            try
            {
                Result = await Task.Run(_work).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Error = ex; // chaqiruvchi turiga qarab xabar ko'rsatadi
            }

            _finished = true;
            DialogResult = Error is null ? DialogResult.OK : DialogResult.Abort;
        };
    }

    /// <summary>Natija (xato bo'lsa — <c>null</c>).</summary>
    public T? Result { get; private set; }

    /// <summary>Xato (muvaffaqiyatli bo'lsa — <c>null</c>).</summary>
    public Exception? Error { get; private set; }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_finished && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true; // Alt+F4 — ish tugashini kutamiz
        }

        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _font.Dispose();
            _icon.Dispose();
        }

        base.Dispose(disposing);
    }
}
