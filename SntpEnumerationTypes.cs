namespace TimeSync
{
    // Leap indicator field values
    public enum LeapIndicator
    {
        NoWarning,          // 0 - no warning
        LastMinute61,       // 1 - last minute has 61 seconds
        LastMinute59,       // 2 - last minute has 59 seconds
        AlarmNoSync         // 3 - alarm condition (clock not synchronized)
    }

    // Mode field values
    public enum Mode
    {
        Reserved,           // 0 - reserved
        SymmetricActive,    // 1 - symmetric active
        SymmetricPassive,   // 2 - symmetric passive
        Client,             // 3 - client
        Server,             // 4 - server
        Broadcast           // 5 - broadcast
                            // 6 - reserved for NTP control message
                            // 7 - reserved for private use
    }

    // Stratum field values
    public enum Stratum
    {
        KissOfDeath,        // 0        kiss-o'-death message
        Primary,            // 1        primary reference (e.g., synchronized by radio clock)
        Secondary,          // 2-15     secondary reference (synchronized by NTP or SNTP)
        Reserved            // 16-255   reserved
    }
}
