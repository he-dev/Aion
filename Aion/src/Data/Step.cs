using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Aion.Data;

[UsedImplicitly]
public class Step
{
    [DefaultValue(true)]
    public bool Enabled { get; set; }

    [JsonRequired]
    public string FileName { get; set; } = null!;

    public string? Arguments { get; set; }

    [DefaultValue(ProcessWindowStyle.Hidden)]
    public ProcessWindowStyle WindowStyle { get; set; }

    [DefaultValue(true)]
    public bool WaitForExit { get; set; }
    
    [DefaultValue(true)]
    public bool SingleInstance { get; set; }

    [DefaultValue(OnError.Break)]
    public OnError OnError { get; set; }
}

public enum OnError
{
    Break,
    Continue
}