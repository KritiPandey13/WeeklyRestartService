using Microsoft.Extensions.Hosting;
using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

public class Worker : BackgroundService
{
    private readonly string logPath;
    private readonly string screenshotPath;
    private readonly DayOfWeek scheduledDay;
    private readonly TimeSpan restartTime;

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
            LogEvent("System resumed from sleep/hibernate.");
            TakeScreenshot("SystemResume");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            LogRestartMarkerOnStartup(); // Log the last restart marker if available
            LogEvent("Service started, monitoring for scheduled restarts and user activities.");

            bool userLoginHandled = false; // Flag to ensure user login actions are handled only once
            bool userRestartHandled = false; // Flag to ensure user-triggered restart actions are handled only once

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    DateTime now = DateTime.Now;

                    // Monitor for user login
                    if (Environment.UserInteractive && lastRestartTime != DateTime.MinValue && !userLoginHandled)
                    {
                        LogEvent("User login detected. Creating logs and taking a screenshot...");
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Allow time for login stabilization
                        TakeScreenshot("UserLogin");
                        LogProcessesAndApplications("UserLogin");
                        userLoginHandled = true; // Mark login handling as complete
                    }

                    // Check for scheduled restart
                    if (IsScheduledRestartDue(now))
                    {
                        LogEvent("Scheduled restart time reached.");
                        TakeScreenshot("BeforeRestart");
                        LogProcessesAndApplications("BeforeRestart");
                        await PerformRestartSequence();

                        // Reset flags after a restart
                        userLoginHandled = false;
                        userRestartHandled = false;
                    }

                    // Monitor for user-triggered restart
                    if (Environment.TickCount < 150000 && lastRestartTime != DateTime.MinValue && !userRestartHandled)
                    {
                        LogEvent("Detected user-triggered restart.");
                        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); // Allow time for post-restart stabilization
                        TakeScreenshot("UserRestart");
                        LogProcessesAndApplications("UserRestart");
                        userRestartHandled = true; // Mark restart handling as complete
                        lastRestartTime = DateTime.Now; // Update the last restart time
                    }

                    await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); // Short interval for responsiveness
                }
                catch (Exception ex)
                {
                    LogEvent($"Error in service loop: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
        catch {
            LogEvent($"Error in service loop:Service is restarting.");
        }
    }

    private bool IsScheduledRestartDue(DateTime now)
    {
        return now.DayOfWeek == scheduledDay &&
               now.TimeOfDay >= restartTime &&
               now.TimeOfDay < restartTime.Add(TimeSpan.FromMinutes(1));
    }

    private async Task PerformRestartSequence()
    {
        try
        {
            LogEvent("Performing scheduled restart.");

            //LogProcessesAndApplications("BeforeRestart");
            //TakeScreenshot("BeforeRestart");

            LogEvent("Service is stopping. System will restart shortly.");
            RecordRestartMarker(); // Write restart marker to a file
            Process.Start("shutdown", "/r /t 30");
            lastRestartTime = DateTime.Now;

            await Task.Delay(TimeSpan.FromMinutes(3)); // Simulate waiting for restart
        }
        catch (Exception ex)
        {
            LogEvent($"Error during restart sequence: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void RecordRestartMarker()
    {
        string filePath = Path.Combine(logPath, "RestartMarker.txt");
        File.WriteAllText(filePath, $"Last restart: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        LogEvent($"Restart marker written to file: {filePath}");
    }

    private void LogRestartMarkerOnStartup()
    {
        string filePath = Path.Combine(logPath, "RestartMarker.txt");
        if (File.Exists(filePath))
        {
            string restartMarker = File.ReadAllText(filePath);
            LogEvent($"Service resumed after restart. {restartMarker}");
            LogProcessesAndApplications("AfterRestart");
            TakeScreenshot("AfterRestart");

            File.Delete(filePath); // Clean up after reading
        }
    }

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
