# TimeSync
A Simple Network Time Protocol (SNTP) client based on [RFC 4330](https://datatracker.ietf.org/doc/html/rfc4330).

Initially based on the [RFC 2030](https://datatracker.ietf.org/doc/html/rfc2030) implementations of Valer Bocan ([here](https://www.codeproject.com/Articles/1005/SNTP-Client-in-C) and [here](https://github.com/vbocan/sntp-client)), the code was updated to RFC 4330 with an option for the user to specify a NTP/SNTP server.

#### Help

```
Time Synchronizer (SNTP Client)
(C)2001-2019 Valer BOCAN, PhD <valer@bocan.ro>
Modified by Miguel GARCIA-BLANCO [2021/11/05 16:29Z]

Set the system date and time from a remote NTP time server.

Usage: TimeSync [-q] [SERVER]
  -q, --query   Query only - do not set the clock
  -?, --help    Display this help and exit

If SERVER is not specified, the default server 'pool.ntp.org' will be used.

Examples:
  TimeSync -q
  TimeSync time.nist.gov
  TimeSync 128.138.141.172
```

#### Example

```
Time Synchronizer (SNTP Client)
(C)2001-2019 Valer BOCAN, PhD <valer@bocan.ro>
Modified by Miguel GARCIA-BLANCO [2021/11/05 16:29Z]

Connecting to pool.ntp.org...

 Leap Indicator: 0 (no warning)
 NTP Version: 4
 Stratum: 2 (secondary reference - synchronized by NTP or SNTP)
 Poll Interval: 3 (8 s)
 Precision: -24 (59.605 ns)
 Root Delay: 4.364 ms
 Root Dispersion: 38.864 ms
 Reference ID: 192.33.96.101
 Local Time: 2023/01/01 19:13:40.753+10:30
 Round Trip Delay: 351.26 ms
 System Clock Offset: -66.16 ms

SYSTEM TIME NOT UPDATED
```

---
### References
* [RFC 4330 - Simple Network Time Protocol (SNTP) Version 4 for IPv4, IPv6 and OSI](https://datatracker.ietf.org/doc/html/rfc4330)
* [SNTP Client in C#](https://www.codeproject.com/Articles/1005/SNTP-Client-in-C) - Valer Bocan
* [C# SNTP client (the very one used by Microsoft in .NET Micro Framework)](https://github.com/vbocan/sntp-client) - Valer Bocan
