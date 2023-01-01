namespace TimeSync
{
    using System.Runtime.InteropServices; // StructLayoutAttribute

    // SYSTEMTIME structure used by SetSystemTime()
    //   <https://docs.microsoft.com/en-us/windows/win32/api/minwinbase/ns-minwinbase-systemtime>
    //   <https://stackoverflow.com/a/6083225>
    //   <https://stackoverflow.com/a/650872>
    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEMTIME
    {
        public ushort wYear;
        public ushort wMonth;
        public ushort wDayOfWeek; // Ignored by SetSystemTime() and SetLocalTime()
        public ushort wDay;
        public ushort wHour;
        public ushort wMinute;
        public ushort wSecond;
        public ushort wMilliseconds;
    }
}
