using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

public class Worker : BackgroundService
{
    private readonly string logPath;
    private readonly string screenshotPath;
    private readonly DayOfWeek scheduledDay;
    private readonly TimeSpan restartTime;

    private bool screenAwake = false;
    private DateTime lastRestartTime = DateTime.MinValue;

    public Worker(IConfiguration configuration)
    {
        logPath = configuration["Logging:LogPath"] ?? @"C:\Logs\TextLog";
        screenshotPath = configuration["Logging:ScreenshotPath"] ?? @"C:\Logs\ScreenshotLog";

        Directory.CreateDirectory(logPath);
        Directory.CreateDirectory(screenshotPath);

        string scheduledDayConfig = configuration["RestartSettings:ScheduledDay"] ?? "Sunday";
        string restartTimeConfig = configuration["RestartSettings:RestartTime"] ?? "03:00";

        scheduledDay = Enum.TryParse(scheduledDayConfig, true, out DayOfWeek day) ? day : DayOfWeek.Sunday;
        restartTime = TimeSpan.TryParse(restartTimeConfig, out TimeSpan time) ? time : new TimeSpan(3, 0, 0);

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            LogEvent("System resumed from sleep/hibernate. Preparing to take screenshot.");
            screenAwake = true;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogEvent("Service started, monitoring for scheduled restarts and user activities.");

        while (!stoppingToken.IsCancellationRequested)
        {
            DateTime now = DateTime.Now;

            // Monitor for user login
            if (Environment.UserInteractive && lastRestartTime != DateTime.MinValue)
            {
                LogEvent("User has logged in. Creating logs after a minute...");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                TakeScreenshot("UserLogin");
                LogProcessesAndApplications("UserLogin");
            }

            // Check for scheduled restart
            if (now.DayOfWeek == scheduledDay && now.TimeOfDay >= restartTime && now.TimeOfDay < restartTime.Add(TimeSpan.FromMinutes(1)))
            {
                LogEvent("Scheduled restart time reached.");
                TakeScreenshot("BeforeRestart");
                LogProcessesAndApplications("BeforeRestart");
                await PerformRestartSequence();
            }

            // Monitor for user-triggered restart
            if (Environment.TickCount < 150000 && lastRestartTime != DateTime.MinValue)
            {
                LogEvent("Detected user-triggered restart.");
                TakeScreenshot("UserRestart");
                LogProcessesAndApplications("UserRestart");
                lastRestartTime = DateTime.Now;
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }


    private async Task PerformRestartSequence()
    {
        try
        {
            LogEvent("Scheduled restart triggered.");

            LogProcessesAndApplications("BeforeRestart");
            //TakeScreenshot("BeforeRestart");

            Process.Start("shutdown", "/r /t 30");
            LogEvent("System restart command issued.");
            lastRestartTime = DateTime.Now;

            await Task.Delay(TimeSpan.FromMinutes(1)); // Wait for restart
            LogEvent("Wait for restart.");
            // Simulate post-restart actions
            await Task.Delay(TimeSpan.FromMinutes(3));

            LogProcessesAndApplications("AfterRestart");
            //TakeScreenshot("AfterRestart");

            LogEvent("Scheduled restart completed successfully.");
        }
        catch (Exception ex)
        {
            LogEvent($"Error during scheduled restart: {ex.Message}\n{ex.StackTrace}");
        }
    }

    //private bool IsScheduledRestartDue()
    //{
    //    var now = DateTime.Now;
    //    return now.DayOfWeek == scheduledDay &&
    //           now.TimeOfDay >= restartTime &&
    //           now.TimeOfDay < restartTime.Add(TimeSpan.FromMinutes(1)) &&
    //           lastRestartTime.Date != now.Date;
    //}

    private void TakeScreenshot(string prefix)
    {
        try
        {
            if (!Environment.UserInteractive)
            {
                LogEvent("Screenshot skipped: Not running in an interactive session.");
                return;
            }

            string fileName = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string filePath = Path.Combine(screenshotPath, fileName);

            // Get the screen dimensions
            int screenWidth = Screen.PrimaryScreen.Bounds.Width;
            int screenHeight = Screen.PrimaryScreen.Bounds.Height;

            using Bitmap bitmap = new Bitmap(screenWidth, screenHeight);
            using Graphics g = Graphics.FromImage(bitmap);
            g.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

            LogEvent($"Screenshot taken: {filePath}");
        }
        catch (Exception ex)
        {
            LogEvent($"Error taking screenshot: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void LogProcessesAndApplications(string prefix)
    {
        try
        {
            string logFile = Path.Combine(logPath, $"{prefix}_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            using var writer = new StreamWriter(logFile);

            var processes = Process.GetProcesses()
                                   .OrderBy(p => p.ProcessName)
                                   .Where(p => !string.IsNullOrEmpty(p.ProcessName))
                                   .ToList();

            writer.WriteLine($"Processes running ({processes.Count}):");
            foreach (var process in processes)
            {
                try
                {
                    writer.WriteLine($"- {process.ProcessName} (ID: {process.Id})");
                }
                catch
                {
                    LogEvent("Error processing the process list.");
                }
            }

            writer.WriteLine();

            var applications = processes.Where(p =>
            {
                try
                {
                    return !string.IsNullOrEmpty(p.MainWindowTitle);
                }
                catch
                {
                    return false;
                }
            }).OrderBy(p => p.ProcessName);

            writer.WriteLine($"Applications running ({applications.Count()}):");
            foreach (var app in applications)
            {
                writer.WriteLine($"- {app.ProcessName} (ID: {app.Id}) - Title: {app.MainWindowTitle}");
            }

            LogEvent($"Logged processes and applications in: {logFile}");
        }
        catch (Exception ex)
        {
            LogEvent($"Error logging processes and applications: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void LogEvent(string message)
    {
        try
        {
            string logFile = Path.Combine(logPath, $"EventLog_{DateTime.Now:yyyyMMdd}.txt");
            File.AppendAllText(logFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to write log: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
