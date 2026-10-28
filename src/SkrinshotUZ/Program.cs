using SkrinshotUZ.Core.Startup;

namespace SkrinshotUZ;

/// <summary>Kirish nuqtasi.</summary>
internal static class Program
{
    /// <summary>
    /// Bitta nusxa qulfi. "Local\" — har bir Windows foydalanuvchi seansi uchun alohida
    /// (maktab kompyuterida bir nechta o'quvchi akkaunti bo'lishi mumkin).
    /// </summary>
    private const string SingleInstanceMutexName = @"Local\SkrinshotUZ.SingleInstance.6C3F2A1E";

    /// <summary>Ikkinchi nusxa birinchisiga "men ochilmoqchi edim" deb signal beradi.</summary>
    private const string ActivateEventName = @"Local\SkrinshotUZ.Activate.6C3F2A1E";

    /// <summary>O'rnatuvchi (yangilash/o'chirish) ishlab turgan nusxaga "yopil" deb signal beradi.</summary>
    private const string ExitEventName = @"Local\SkrinshotUZ.Exit.6C3F2A1E";

    /// <summary>Ishlab turgan nusxa yopilishini kutish vaqti.</summary>
    private static readonly TimeSpan ExitWait = TimeSpan.FromSeconds(10);

    private const int ExitOk = 0;
    private const int ExitBadArguments = 2;
    private const int ExitAlreadyRunning = 3;

    [STAThread]
    private static int Main(string[] args)
    {
        StartupOptions options;
        try
        {
            options = StartupOptions.Parse(args);
        }
        catch (ArgumentException)
        {
            return ExitBadArguments;
        }

        // O'rnatuvchi buyruqlari: tray ochilmaydi, bitta nusxa qulfi olinmaydi.
        if (options.Exit || options.Uninstall)
        {
            bool closed = CloseRunningInstance();
            if (options.Uninstall)
            {
                ApplicationConfiguration.Initialize(); // TaskDialog uchun vizual uslublar
                Uninstaller.CleanUp(options.Quiet);
            }

            return closed ? ExitOk : ExitAlreadyRunning;
        }

        // Ikkinchi nusxa ochilmaydi: PrintScreen tugmasini ikki dastur birdaniga ushlamasligi kerak.
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            if (!options.SmokeTest && EventWaitHandle.TryOpenExisting(ActivateEventName, out var signal))
            {
                // Birinchi nusxa "Skrinshot UZ allaqachon ishlayapti" xabarini ko'rsatadi.
                using (signal)
                {
                    signal.Set();
                }
            }

            return options.SmokeTest ? ExitAlreadyRunning : ExitOk;
        }

        try
        {
            // Har qanday kutilmagan xatolik logga yoziladi va foydalanuvchiga o'zbekcha tushuntiriladi.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => CrashHandler.Handle(e.Exception, options);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => CrashHandler.Handle(e.ExceptionObject as Exception, options);

            // PerMonitorV2 DPI, vizual uslublar (TaskDialog uchun shart), standart shrift.
            ApplicationConfiguration.Initialize();

            using var activateSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
            using var exitSignal = new EventWaitHandle(false, EventResetMode.ManualReset, ExitEventName);
            using var context = new TrayApplicationContext(options, activateSignal, exitSignal);
            if (options.SmokeTest)
            {
                SmokeTest.Attach(context, options.SmokeReportPath);
            }

            Application.Run(context);
            return ExitOk;
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    /// <summary>
    /// Ishlab turgan nusxani yopadi (o'rnatuvchi faylni almashtirishi yoki o'chirishi uchun) va u yopilishini kutadi.
    /// </summary>
    /// <returns>Hech qanday nusxa ishlamayotgan bo'lsa yoki u yopilgan bo'lsa <c>true</c>.</returns>
    private static bool CloseRunningInstance()
    {
        if (!Mutex.TryOpenExisting(SingleInstanceMutexName, out var running))
        {
            return true;
        }

        using (running)
        {
            if (EventWaitHandle.TryOpenExisting(ExitEventName, out var signal))
            {
                using (signal)
                {
                    signal.Set();
                }
            }

            try
            {
                // Mutex bo'shasa (yoki egasi yopilib "tashlab ketilgan" bo'lsa) — nusxa yopildi.
                if (running.WaitOne(ExitWait))
                {
                    running.ReleaseMutex();
                    return true;
                }

                return false;
            }
            catch (AbandonedMutexException)
            {
                return true;
            }
        }
    }
}
