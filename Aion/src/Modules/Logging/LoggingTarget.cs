using System;

namespace Aion.Modules.Logging;

[Flags]
public enum LoggingTarget
{
    None = 0x0,
    Self = 0x1,
    Main = 0x2
}