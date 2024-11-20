**A Windows Service for Automated System Restarts with Logging and Monitoring**
This project provides a robust Windows Service that performs scheduled system restarts, monitors system events, and logs user activities, running processes, and applications. It is built with .NET Core and is highly configurable through appsettings.json.

**Features**
Automated Scheduled Restarts: Configurable day and time for restarts.
User-Triggered Restart Detection: Logs processes and applications after a manual restart.
_Logging_:
System events.
Running processes and applications.
_Screenshots_:
Captures screenshots before and after restarts.
Saves images in a configurable directory.
_Event Monitoring_:
Detects system resume from sleep/hibernate.
Monitors user login post-restart.
**Installation**
1. _Clone the Repository_
bash

git clone https://github.com/KritiPandey13/WeeklyRestartService.git
2. _Build the Service_
Build the project using Visual Studio or the .NET CLI:

bash

dotnet build
3. _Publish the Executable_
Publish the service as a self-contained executable:

bash

dotnet publish -c Release -o ./publish
4. _Install the Service_
Install the service using PowerShell or the sc command:

bash

sc create WeeklyRestartService binPath="C:\path\to\publish\WeeklyRestartService.exe"
Configuration
Edit appsettings.json
Modify appsettings.json to configure the service:

**json**

{
  "Logging": {
    "LogPath": "C:\\Services\\ServiceLogs\\Logs\\TextLog",
    "ScreenshotPath": "C:\\Services\\ServiceLogs\\Logs\\ScreenshotLog"
  },
  "RestartSettings": {
    "ScheduledDay": "Monday",
    "RestartTime": "03:00"
  }
}
_LogPath_: Directory for saving event logs.
_ScreenshotPath_: Directory for saving screenshots.
_ScheduledDay_: Day of the week for the restart (e.g., "Monday").
_RestartTime_: Time of the restart in HH:mm format (e.g., "03:00").
Usage
Start the Service

Start the service via the Windows Services Manager or using PowerShell:
bash

net start WeeklyRestartService
Monitor Logs

Logs and screenshots are saved in the directories specified in appsettings.json.
Stop the Service

Stop the service via the Windows Services manager or using PowerShell:
bash

net stop WeeklyRestartService
Logs and Screenshots
Logs
**Event Logs**: Captures restart-related events in daily log files.
**Process/Application Logs**: Logs running processes and applications before and after restarts.
Screenshots
**BeforeRestart**: Captured just before a restart.
**AfterRestart**: Captured after a restart.
Development
Prerequisites
.NET SDK (version 8 or later)
Visual Studio or any IDE that supports .NET development
Running Locally
Clone the repository.
Modify appsettings.json for local testing.
Run the service directly for debugging:
bash

dotnet run
Contributing
Fork the repository.
Create a feature branch:
bash

git checkout -b feature/YourFeature
Commit your changes:
bash

git commit -m "Add your feature description"
Push to the branch:
bash

git push origin feature/YourFeature
Open a pull request.


**Contact**
For questions or feedback, feel free to contact:

**Author: Kriti Bajpai Pandey
Email: bajpaikriti.rd@gmail.com**

**Acknowledgments**
Special thanks to contributors and the .NET community for their support and resources.
