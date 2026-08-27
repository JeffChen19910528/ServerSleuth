namespace ServerSleuth.Core.Enums;

[Flags]
public enum PlatformSupport
{
    None = 0,
    Windows = 1,
    Linux = 2,
    Both = Windows | Linux
}
