# Aion v4.0.0

`Aion` _(Greek: Αἰών)_ is a Hellenistic deity associated with time, the orb or circle encompassing the universe. As a tool `Aion` is a cron scheduler that launches robots at the specified time. You install it as a windows service. It's build with the `Quartz` scheduler.

## Configuration

`Aion` requires a minimal configuration. Via the `app.config` file you need to specify three settings. 

- `Environment` - used for logging.
- `Jobs.RobotConfigUpdater.Schedule` - used for robot scheme pulling.
- `Paths.RobotsDirectoryName` - used for robot schema pulling and for launching robots. This is the directory where you _install_ your robots if you want to use a relative path. 

```xml
<appSettings>

    <add key="aion.Environment" value="debug" />
    <add key="aion.Jobs.RobotConfigUpdater.Schedule" value="0/20 * * * * ?"/>
    <add key="aion.Paths.RobotsDirectoryName" value="C:\Home\Temp\Services\Aion-v4\Robots"/>

</appSettings>
```

The second part of the configuration is the robot scheme. It's a `JSON` file that contains the schedule and the list of robots. Settings marked with `<>` are mandatory and those with `[]` are optional. The value shown for optional settings is the default value.

`Aion` will look for `Aion.Schemes.*.json` files in the `Paths.RobotsDirectoryName`. 

```js
{
  "Schedule": "0/12 * * * * ?", // <string> - cron expression
  "Enabled": true, // [bool]
  "StartImmediately": false, // [bool]
  // <object[]> - an array of robots to run
  "Robots": [
    {
      "FileName": "Aion.Robots.TestRobot2.exe", // <string> - an absolute or relative path to the *.exe
      "Enabled": true, // [bool]
      "WindowStyle": "Hidden" // [ProcessWindowStyle]
    }
  ]
}
```

## Installing robots

In order to install a robot you need to put it in the `Paths.RobotsDirectoryName` directory in a folder with the same name as the `*.exe`. Withing this folder you need to create another folder that will be the version of the robot e.g. `v2.0.8`.

Example:

If `Paths.RobotsDirectoryName` = `C:\Robots` and the robot's file name is `robot.exe` and its version is `2.0.8` then the full path `Aion` will look for it is `C:\Robots\robot\v2.0.8\robot.exe`.

## Logging

By default `Aoin` uses the Sql Server for logging and would like to find a table like this one:

```sql
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[AionLog](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Timestamp] [datetime2](7) NOT NULL,
	[Environment] [nvarchar](53) NOT NULL,
	[LogLevel] [nvarchar](53) NOT NULL,
	[Logger] [nvarchar](103) NOT NULL,
	[ThreadId] [int] NOT NULL,
	[ElapsedSeconds] [float] NULL,
	[Message] [nvarchar](max) NULL,
	[Exception] [nvarchar](max) NULL,
 CONSTRAINT [PK_dbo_AionLog] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, FILLFACTOR = 80) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
```



