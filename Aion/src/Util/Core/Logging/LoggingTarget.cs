using System;

namespace Aion.Util.Core.Logging;

[Flags]
public enum LoggingTarget
{
    None = 0x0,
    Self = 0x1,
    Main = 0x2
}