# Aion v3

`Aion` is a cron scheduler. You install it as a Windows Service. It's built on top of the `Quartz` scheduler.

The name comes from the _(Greek: Αἰών)_ `Aion` who is a Hellenistic deity associated with time, the orb or circle encompassing the universe. 

## Configuration


https://github.com/serilog/serilog-expressions
https://github.com/scriban/scriban/blob/master/doc/builtins.md


### `appsettings.json` schema

This file contains two custom sections.

- `Scheduler`

```yaml
Name: string # Required name of the instance.
StartDelay: Timestamp # How long to wait for the scheduler to start.
Shutdown: # Kills the scheduler and lets Windows restart the service.
  Enabled: bool # Whether it is enabled.
  Cron: string # When to kill it.
  ExitCode: int # Service exit-code.
Parameters: # Step execution parameters that are merged from all levels where the last wins.
  string: string
Profiles:
  Name: string # Profile name.
    Path: string # Path where to find this profile.
    Sync: 
      Enabled: boolean # Whether to synchronize this profile.
      Cron: string # Cron expression to synchronize this profile.
    Parameters:
      string: string # Key/value pairs of profile-wide variables.
    Environment:
      string: string # Key/value pairs of profile-wide evnrionment variables.
    Workflows:
      Includes: array # Glob filters that specify which workflows to include in the search.
      Excludes: array # Glob filters that specify which workflows to exclude from the search.
```

- `QuartzServer`

```yaml
StartDelaySeconds: int # Specifies how long to wait before the scheduler takes on its job.

```

You configure `Aion` workflows through JSON files.

### `WorkflowTemplate` schema

Workflow template files support only URL safe names like:

```regex
^[a-zA-Z0-9._~-]+$
```

Their body needs to conform to this:


```yaml
Enabled: boolean # Required if this workflow should be scheduled. Defaults to false.
Cron: string # Required cron-expression.
Parameters: 
  string: string # Name/value pairs.
Environment: # Environment variables to apply to each step.
  string: string # Name/value pairs.
Logging: object # Logging preset or Serilog configuration. See steps.
Steps:
  - Enabled: boolean # Required if this step should be executed. Defaults to false.
    FileName: string | template # Required file-name to execute.
    Arguments: # Arguments to use.
      string: string | array # --arg item1 --arg item2
      string[]: string | array # --arg item1 item2
      string[,]: string | array # --arg item1,item2
      string=[,]: string | array # --arg=item1,item2
      _: string | array # positional args
      $: string | array # raw vaues (no processing)      
    Environment: # Environment variables to apply to each step.
      string: string # Name/value pairs.
    OnFaiure: string | null # break, continue (default)
    Logging: # Std logging settings.    
      Preset: string # Name of the preset.
      Custom: json # Serilog configuration.
      Source: string # Which logging to use: None, Auto, Preset, Custom where Auto picks Custom first.
      Target: string # Specifies where to log: Self and/or Main.
```

## Variables

```yaml

- Scheduler.Name
- Scheduler.Directory
- Profile.Name
- Profile.Path
- Workflow.Name
- Workflow.Mode
- Step.Name
- Step.Index
- Step.TraceId
- Step.SpanId
- Step.Parentid

```

## Default logging folder structure

: `{{ Engine.LogsDirectory }}\\{{ Profile.Name }}\\{{ Workflow.Name }}\\{{ Trigger.Mode }}\\{{ Workflow.Name }}-s{{ step.index }}_.log`


