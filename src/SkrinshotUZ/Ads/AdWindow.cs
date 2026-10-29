using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using SkrinshotUZ.Core.Ads;
using SkrinshotUZ.Core.Localization;
using SkrinshotUZ.Storage;

namespace SkrinshotUZ.Ads;

/// <summary>Yig'ish vaqtida berilgan Supabase sozlamasi (GitHub Variables → MSBuild → AssemblyMetadata).</summary>
internal static class AdsConfig
{
    /// <summary>Sozlama; bo'sh bo'lsa — reklama o'chiq va dastur internetga umuman chiqmaydi.</summary>
    public static AdsEndpoint? Endpoint { get; } = AdsEndpoint.TryCreate(Metadata("AdsSupabaseUrl"), Metadata("AdsSupabaseKey"));

    private static string? Metadata(string key) =>
        typeof(AdsConfig).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == key)?.Value;
}

/// <summary>
/// Reklama oynasi: ekranning pastki o'ng burchagida kichik karta. Fokusni olmaydi (yozayotgan matn uzilmaydi),
/// vazifalar panelida ko'rinmaydi, aniq "Reklama" deb belgilangan, har doim yopiladi va 60 soniyadan keyin o'zi yo'qoladi
/// (sichqoncha ustida turganda kutadi).
/// </summary>
internal sealed class AdWindow : Form
{
    /// <summary>O'zi yopilish vaqti.</summary>
    public static readonly TimeSpan AutoCloseAfter = TimeSpan.FromSeconds(60);

    private const int WsExToolWindow = 0x00000080;
    private const int WsExTopmost = 0x00000008;
    private const int WsExNoActivate = 0x08000000;

    private readonly AdItem _ad;
    private readonly TaskCompletionSource<AdOutcome> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly System.Windows.Forms.Timer _autoClose;
    private readonly Font _font;
    private readonly Font _bold;
    private readonly Font _small;
    private readonly Image? _image;
    private readonly Icon _icon;
    private AdOutcome _outcome = AdOutcome.TimedOut;

    /// <summary>Yangi oyna.</summary>
    /// <param name="ad">Reklama.</param>
    /// <param name="imageBytes">Rasm (PNG/JPG; noto'g'ri bo'lsa — rasmsiz ko'rsatiladi).</param>
    public AdWindow(AdItem ad, byte[]? imageBytes)
    {
        _ad = ad ?? throw new ArgumentNullException(nameof(ad));
        _image = TryDecode(imageBytes);

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        _font = new Font("Segoe UI", 10f);
        _bold = new Font("Segoe UI", 11f, FontStyle.Bold);
        _small = new Font("Segoe UI", 8.5f);
        _icon = AppIcon.LoadSmall();
        Font = _font;
        Text = Strings.AppName + " — " + Strings.Ads_Label;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.White;
        Padding = new Padding(1); // chegara chizig'i uchun
        ClientSize = new Size(360, 100);

        // ---- Sarlavha qatori: ikonka · Skrinshot UZ · "Reklama" · ✕ ----
        var header = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, Height = 34, BackColor = Color.FromArgb(245, 247, 250), Padding = new Padding(8, 4, 4, 4) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var iconBox = new PictureBox { Image = _icon.ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(18, 18), Margin = new Padding(0, 4, 6, 0) };
        var appName = new Label { Text = Strings.AppName, AutoSize = true, Font = _small, ForeColor = Color.FromArgb(70, 70, 70), Margin = new Padding(0, 5, 6, 0) };
        var badge = new Label
        {
            Text = Strings.Ads_Label,
            AutoSize = true,
            Font = _small,
            ForeColor = Color.FromArgb(120, 90, 0),
            BackColor = Color.FromArgb(255, 241, 196),
            Padding = new Padding(5, 1, 5, 1),
            Margin = new Padding(0, 4, 0, 0),
        };
        var close = new Button
        {
            Text = "✕",
            FlatStyle = FlatStyle.Flat,
            Size = new Size(30, 26),
            Margin = new Padding(0),
            TabStop = false,
            Cursor = Cursors.Hand,
        };
        close.FlatAppearance.BorderSize = 0;
        close.Click += (_, _) => Finish(AdOutcome.Dismissed);
        var tips = new ToolTip();
        tips.SetToolTip(close, Strings.Ads_Close);
        header.Controls.Add(iconBox, 0, 0);
        header.Controls.Add(appName, 1, 0);
        header.Controls.Add(badge, 2, 0);
        header.Controls.Add(close, 3, 0);

        // ---- Tana: rasm, sarlavha, tugmalar ----
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12, 10, 12, 10), AutoSize = true };
        int contentWidth = 336;
        if (_image is not null)
        {
            int h = Math.Clamp((int)Math.Round(contentWidth * (double)_image.Height / _image.Width), 40, 200);
            var picture = new PictureBox { Image = _image, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(contentWidth, h), Margin = new Padding(0, 0, 0, 8), Cursor = ad.LinkUrl is null ? Cursors.Default : Cursors.Hand };
            if (ad.LinkUrl is not null)
            {
                picture.Click += (_, _) => OpenLink();
            }

            body.Controls.Add(picture);
        }

        var title = new Label { Text = ad.Title, Font = _bold, AutoSize = true, MaximumSize = new Size(contentWidth, 0), Margin = new Padding(0, 0, 0, 10), UseMnemonic = false };
        body.Controls.Add(title);

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0), WrapContents = false };
        if (ad.LinkUrl is not null)
        {
            var more = new Button { Text = Strings.Ads_More, AutoSize = true, MinimumSize = new Size(110, 34), BackColor = Color.FromArgb(0, 103, 192), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = _bold, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 0) };
            more.FlatAppearance.BorderSize = 0;
            more.Click += (_, _) => OpenLink();
            tips.SetToolTip(more, Strings.Ads_MoreTip);
            buttons.Controls.Add(more);
        }

        var dismiss = new Button { Text = Strings.Ads_Close, AutoSize = true, MinimumSize = new Size(90, 34), FlatStyle = FlatStyle.System, Margin = new Padding(0) };
        dismiss.Click += (_, _) => Finish(AdOutcome.Dismissed);
        buttons.Controls.Add(dismiss);
        body.Controls.Add(buttons);

        Controls.Add(body);
        Controls.Add(header);
        ResumeLayout(false);
        PerformLayout();

        ClientSize = new Size(ClientSize.Width, header.Height + body.PreferredSize.Height + 2);

        _autoClose = new System.Windows.Forms.Timer { Interval = (int)AutoCloseAfter.TotalMilliseconds };
        _autoClose.Tick += (_, _) =>
        {
            // Sichqoncha ustida bo'lsa — o'qiyotgan bo'lishi mumkin, kutamiz.
            if (!Bounds.Contains(Cursor.Position))
            {
                Finish(AdOutcome.TimedOut);
            }
        };
        Disposed += (_, _) => tips.Dispose();
    }

    /// <summary>Oyna yopilganda tugaydi.</summary>
    public Task<AdOutcome> Completion => _completion.Task;

    /// <summary>Rasm ko'rsatildimi (test va tutun testi uchun).</summary>
    internal bool HasImage => _image is not null;

    /// <summary>Oyna uslublari (test uchun: fokus olmasligi, vazifalar panelida ko'rinmasligi).</summary>
    internal int ExtendedStyleForTest => CreateParams.ExStyle;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExToolWindow | WsExTopmost | WsExNoActivate;
            return cp;
        }
    }

    /// <summary>Ekranning pastki o'ng burchagiga joylashtiradi (vazifalar paneli ustida).</summary>
    public void PlaceInCorner(Rectangle workingArea)
    {
        int margin = (int)Math.Round(12 * DeviceDpi / 96.0);
        Location = new Point(workingArea.Right - Width - margin, workingArea.Bottom - Height - margin);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _autoClose.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Color.FromArgb(190, 196, 204));
        e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _autoClose.Stop();
        _completion.TrySetResult(_outcome);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _autoClose.Dispose();
            _image?.Dispose();
            _font.Dispose();
            _bold.Dispose();
            _small.Dispose();
            _icon.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Rasm baytlarini o'qiydi (buzilgan, juda katta yoki noma'lum format — <c>null</c>).</summary>
    internal static Image? TryDecode(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var decoded = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
            if (decoded.Width < 1 || decoded.Height < 1 || decoded.Width > 4000 || decoded.Height > 4000)
            {
                return null;
            }

            // Kichraytirilgan nusxa: oynada 336 px kenglikdan katta kerak emas (xotira tejaladi).
            double scale = Math.Min(1.0, 720.0 / decoded.Width);
            var copy = new Bitmap(Math.Max(1, (int)(decoded.Width * scale)), Math.Max(1, (int)(decoded.Height * scale)));
            using var g = Graphics.FromImage(copy);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(decoded, 0, 0, copy.Width, copy.Height);
            return copy;
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException)
        {
            return null;
        }
    }

    /// <summary>Foydalanuvchi tanlovisiz yopish (skrinshot olinayotganda) — statistikada "bosildi" hisoblanmaydi.</summary>
    public void CloseSilently() => Finish(AdOutcome.TimedOut);

    private void OpenLink()
    {
        if (_ad.LinkUrl is string url && AdFeed.SafeUrl(url) is string safe)
        {
            Shell.OpenUrl(safe);
            Finish(AdOutcome.Clicked);
        }
    }

    private void Finish(AdOutcome outcome)
    {
        _outcome = outcome;
        Close();
    }
}

/// <summary><see cref="IAdPresenter"/> — Windows oynasi (UI oqimida chaqiriladi).</summary>
internal sealed class WindowsAdPresenter : IAdPresenter
{
    public Task<AdOutcome> ShowAsync(AdItem ad, byte[]? image)
    {
        var window = new AdWindow(ad, image);
        window.PlaceInCorner(Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea);
        window.FormClosed += (_, _) => window.Dispose();
        window.Show();
        return window.Completion;
    }
}
