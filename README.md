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
Variables: #
  string: string
Profiles:
  - Path: string # Path where to find this profile.
    Sync: string # Cron expression to synchronize this profile.
    SyncEnabled: boolean # Whether to synchronize this profile.
    Variables:
      string: string # Key/value pairs of profile-wide variables.
    Environment:
      string: string # Key/value pairs of profile-wide evnrionment variables.
    Includes: array # Glob filters that specify which workflows to include in the search.
    Excludes: array # Glob filters that specify which workflows to exclude from the search.
    Logging:
      Preset:
        Name: string # Specifies the logging preset for this profile.

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
Variables: # Workflow variables.
  string: string # Name/value pairs.
Environment: # Environment variables to apply to each step.
  string: string # Name/value pairs.
Logging: json # Logging preset or Serilog configuration.
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
    DependsOn: $previous | array<int> # Whether this step depends on the result of the previous one.
    OnFaiure: string | null # break, continue (default)
    Logging: # Std logging settings.    
      source: string | null # None, Auto, Preset, Custom
      preset: string | null # Preset name.
      custom: object | null # Serilog configuration.
      global: bool # Whether to inlcude the output in the global log.
      target: string # Self (default), Main - Where to inlcude the output.
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


