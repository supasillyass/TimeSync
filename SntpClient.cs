/* PRIMARY REFERENCE <https://www.codeproject.com/Articles/1005/SNTP-Client-in-C>
 * NTPClient
 * Copyright (C)2001 Valer BOCAN <vbocan@dataman.ro>
 * Last modified: June 29, 2001
 * All Rights Reserved
 *
 * This code is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY, without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 *
 * To fully understand the concepts used herein, I strongly
 * recommend that you read the RFC 2030. <https://datatracker.ietf.org/doc/html/rfc2030>
 *
 * NOTE: This example is intended to be compiled with Visual Studio .NET Beta 2
 */

/* SUPPLEMENTARY REFERENCE <https://github.com/vbocan/sntp-client>
 * The C# SNTP client used by Microsoft in .NET Micro Framework
 *
 * Copyright (C)2001-2019 Valer BOCAN, PhD <valer@bocan.ro>
 * Last modified: August 3rd, 2019
 */

/*
 * Updated to RFC 4330 by Miguel GARCIA-BLANCO
 * Last modified: November 6, 2021
 *
 * Simple Network Time Protocol (SNTP) Version 4 for IPv4, IPv6 and OSI
 * <https://datatracker.ietf.org/doc/html/rfc4330>
 */

namespace TimeSync
{
    using System;
    using System.ComponentModel;          // Win32Exception()
    using System.Net;                     // Dns, IPAddress, IPEndPoint, IPHostEntry
    using System.Net.Sockets;             // UdpClient
    using System.Runtime.InteropServices; // Marshal.GetLastWin32Error()
    using System.Threading.Tasks;         // Task
/* BEGIN:DEBUG ****************************************************************/
// using System.Diagnostics; // Stopwatch <https://stackoverflow.com/a/16376269>
/* END:DEBUG ******************************************************************/

    /// <summary>
    /// SntpClient is a C# class designed to connect to time servers on the
    /// Internet and update the system clock. The implementation of the protocol
    /// is based on RFC 4330. <https://datatracker.ietf.org/doc/html/rfc4330>
    ///
    /// Class members:
    ///   LeapIndicator - Warns of an impending leap second to be inserted or
    ///      deleted in the last minute of the current day.
    ///
    ///   VersionNumber - NTP/SNTP version number, currently 4.
    ///
    ///   Mode - Protocol mode (see the Mode enum).
    ///
    ///   StratumCouple - Stratum of the server (see the Stratum enum).
    ///
    ///   PollIntervalCouple - Maximum interval between successive messages.
    ///
    ///   PrecisionCouple - Precision of the system clock.
    ///
    ///   RootDelay - Total roundtrip delay to the primary reference source.
    ///
    ///   RootDispersion - Maximum error due to the clock frequency tolerance.
    ///
    ///   ReferenceIdentifier - Reference identifier (a four-character ASCII
    ///      string, an IPv4 address or MD5 hash).
    ///
    ///   ReferenceTimestamp - Time the system clock was last set or corrected.
    ///      [MGB: 28/09/2021] The documentation variously refers to this as the
    ///      last correction time of the "local clock" (RFC 2030) or the "system
    ///      clock" (RFC 4330), but observation strongly suggests it is actually
    ///      the last time the reference clock (server) itself was corrected.
    ///
    ///   OriginateTimestamp - Time at which the request departed the client for
    ///      the server.
    ///
    ///   ReceiveTimestamp - Time at which the request arrived at the server or
    ///      the reply arrived at the client.
    ///
    ///   TransmitTimestamp - Time at which the request departed the client or
    ///      the reply departed the server.
    ///
    ///   RoundtripDelay - Time between the departure of request and arrival of
    ///      reply.
    ///
    ///   SystemClockOffset - Offset of the local clock relative to the primary
    ///      reference source.
    ///
    ///   GetUtcFromTimestamp - Converts an NTP timestamp into UTC.
    ///
    ///   SetTimestamp - Sets a timestamp in the NTP packet header.
    ///
    ///   ConnectToTimeServer - Connects to the time server and sends/receives
    ///      the NTP message.
    ///
    ///   InitializeNtpPacketHeader - Initializes the NTP packet header.
    ///
    ///   ValidateServerResponse - Checks the validity of the received data and
    ///      if it comes from a NTP-compliant time server.
    ///
    ///   SetSystemClock - Sets the system time.
    ///
    ///   PrintData - Displays the data received from the time server.
    ///
    ///-------------------------------------------------------------------------
    /// NTP Timestamp Format <https://is.gd/fiurGz>
    ///
    ///    ┌───────────────┬───────────────┬───────────────┬───────────────┐
    ///    │   octet[0]    │   octet[1]    │   octet[2]    │   octet[3]    │
    ///    │               ╵               ╵               ╵               │
    ///    │0                   1                   2                   3  │
    ///    │0 1 2 3 4 5 6 7╷8 9 0 1 2 3 4 5╷6 7 8 9 0 1 2 3╷4 5 6 7 8 9 0 1│
    ///   ─┼─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┴─┤
    ///  0 │                            Seconds                            │
    ///    ├───────────────────────────────────────────────────────────────┤
    ///  4 │                  Seconds Fraction (0-padded)                  │
    ///   ─┼─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┤
    ///    ╵               ╵               ╵               ╵               ╵
    ///
    ///-------------------------------------------------------------------------
    /// Message Format - NTP Packet Header <https://is.gd/7ChuiC>
    ///
    ///    ┌───────────────┬───────────────┬───────────────┬───────────────┐
    ///    │   octet[0]    │   octet[1]    │   octet[2]    │   octet[3]    │
    ///    │               ╵               ╵               ╵               │
    ///    │0                   1                   2                   3  │
    ///    │0 1 2 3 4 5 6 7╷8 9 0 1 2 3 4 5╷6 7 8 9 0 1 2 3╷4 5 6 7 8 9 0 1│
    ///   ─┼─┴─┼─┴─┴─┼─┴─┴─┼─┴─┴─┴─┴─┴─┴─┴─┼─┴─┴─┴─┴─┴─┴─┴─┼─┴─┴─┴─┴─┴─┴─┴─┤
    ///  0 │LI │ VN  │Mode │    Stratum    │     Poll      │   Precision   │
    ///    ├───┴─────┴─────┴───────────────┴───────────────┴───────────────┤
    ///  4 │                          Root Delay (32)                      │
    ///    ├───────────────────────────────────────────────────────────────┤
    ///  8 │                        Root Dispersion (32)                   │
    ///    ├───────────────────────────────────────────────────────────────┤
    /// 12 │                     Reference Identifier (32)                 │
    ///    ├───────────────────────────────────────────────────────────────┤
    /// 16 │                                                               │
    ///    │                      Reference Timestamp (64)                 │
    ///    │                                                               │
    ///    ├───────────────────────────────────────────────────────────────┤
    /// 24 │                                                               │
    ///    │                      Originate Timestamp (64)                 │
    ///    │                                                               │
    ///    ├───────────────────────────────────────────────────────────────┤
    /// 32 │                                                               │
    ///    │                       Receive Timestamp (64)                  │
    ///    │                                                               │
    ///    ├───────────────────────────────────────────────────────────────┤
    /// 40 │                                                               │
    ///    │                      Transmit Timestamp (64)                  │
    ///    │                                                               │
    ///    ├───────────────────────────────────────────────────────────────┤
    ///    │                   Key Identifier (optional) (32)              │
    ///    ├───────────────────────────────────────────────────────────────┤
    ///    │                                                               │
    ///    │                                                               │
    ///    │                   Message Digest (optional) (128)             │
    ///    │                                                               │
    ///    │               ╷               ╷               ╷               │
    ///   ─┼─┴─┴─┴─┴─┴─┴─┴─┼─┴─┴─┴─┴─┴─┴─┴─┼─┴─┴─┴─┴─┴─┴─┴─┼─┴─┴─┴─┴─┴─┴─┴─┤
    /// </summary>

    public class SntpClient
    {
/* BEGIN:DEBUG ****************************************************************/
// public Stopwatch stopwatch = new Stopwatch();
// Usage:
//   stopwatch.Start();
//   stopwatch.Stop();
// //Console.ForegroundColor = ConsoleColor.Magenta;
//   Console.WriteLine("Stopwatch: {0:F4} ms", (stopwatch.Elapsed).TotalMilliseconds);
// //Console.ResetColor();
/* END:DEBUG ******************************************************************/

        // The UDP port number assigned by the IANA to NTP is 123.  The SNTP
        // client should use this value in the UDP Destination Port field for
        // client request messages.
        private const ushort NtpPort = 123;

        // NTP packet header length (excluding Key Identifier and Message Digest)
        private const byte NtpPacketHeaderLength = 48; // octets

        // NTP packet header (as described in RFC 4330 <https://is.gd/7ChuiC>)
        private byte[] NtpPacketHeader = new byte[NtpPacketHeaderLength];

        // Offset constants for timestamps in the packet header
        private static class Offset
        {
            public const byte RootDelay           =  4;
            public const byte RootDispersion      =  8;
            public const byte ReferenceIdentifier = 12;
            public const byte ReferenceTimestamp  = 16;
            public const byte OriginateTimestamp  = 24;
            public const byte ReceiveTimestamp    = 32;
            public const byte TransmitTimestamp   = 40;
        }

        // NTP timestamps are represented as a 64-bit unsigned fixed-point
        // number, in seconds relative to 0h on 1 January 1900 UTC (prime epoch).
        private static readonly DateTime NtpEpoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // Note that since some time in 1968 (second 2,147,483,648), the most
        // significant bit (bit 0 of the integer part) has been set and that the
        // 64-bit field will overflow some time in 2036 (second 4,294,967,296).
        // There will exist a 232-picosecond interval, henceforth ignored, every
        // 136 years when the 64-bit field will be 0, which by convention is
        // interpreted as an invalid or unavailable timestamp.
        //
        //    As the NTP timestamp format has been in use for over 20 years, it
        //    is possible that it will be in use 32 years from now, when the
        //    seconds field overflows.  As it is probably inappropriate to
        //    archive NTP timestamps before bit 0 was set in 1968, a convenient
        //    way to extend the useful life of NTP timestamps is the following
        //    convention: If bit 0 is set, the UTC time is in the range 1968-
        //    2036, and UTC time is reckoned from 0h 0m 0s UTC on 1 January
        //    1900.  If bit 0 is not set, the time is in the range 2036-2104 and
        //    UTC time is reckoned from 6h 28m 16s UTC on 7 February 2036.  Note
        //    that when calculating the correspondence, 2000 is a leap year, and
        //    leap seconds are not included in the reckoning.

        // Maximum allowed time disparity between client and server (34 years)
        private const int MaxYearsDisparity = 34;
        private const int DaysPerYear = 365; // without leap days
        private static readonly TimeSpan MaxTimeDisparity = TimeSpan.FromDays(MaxYearsDisparity * DaysPerYear);
        // The arithmetic calculations used by NTP to determine the clock offset
        // and roundtrip delay require the client time to be within 34 years of
        // the server time before the client is launched.

        // Time limit to prevent UdpClient.[Send|Receive]() blocking
        private const int UdpTimeout = 5000; // milliseconds

        // Time limit to prevent Dns.GetHostEntry() blocking
        private const int DnsTimeout = 250; // milliseconds
        // It typically takes 20-120 milliseconds for DNS to lookup the IP
        // address for a given hostname. <https://is.gd/ukH4AX>

        // System clock offset threshold for updating system time
        private const int UpdateThreshold = 50; // milliseconds

        // Additive correction for 'DateTime.Millisecond' rounding
        private const long TruncationCorrection = TimeSpan.TicksPerMillisecond / 2;
        // 'DateTime.Millisecond' truncates fractional milliseconds. By adding
        // the correction (0.5 ms = 5,000 ticks), the fractional component gets
        // rounded to the nearest millisecond.

        // Leap Indicator (LI): This is a two-bit code warning of an impending
        // leap second to be inserted/deleted in the last minute of the current
        // day.  This field is significant only in server messages, where the
        // values are defined as follows:
        //
        //    LI       Meaning
        //    ─────────────────────────────────────────────
        //    0        no warning
        //    1        last minute has 61 seconds
        //    2        last minute has 59 seconds
        //    3        alarm condition (clock not synchronized)
        //
        // On startup, servers set this field to 3 (clock not synchronized), and
        // set this field to some other value when synchronized to the primary
        // reference clock.  Once set to a value other than 3, the field is
        // never set to that value again, even if all synchronization sources
        // become unreachable or defective.
        private LeapIndicator LeapIndicator
        {
            get
            {
                var octet00 = NtpPacketHeader[0];

                // Get two highest bits: 0b_bb_###### --> 0b_000000_bb
                var onMask = 0b_11_000000;
                var rightShift = 6;
                var val = (octet00 & onMask) >> rightShift;

                switch (val)
                {
                    case 0:
                        return LeapIndicator.NoWarning;
                    case 1:
                        return LeapIndicator.LastMinute61;
                    case 2:
                        return LeapIndicator.LastMinute59;
                    case 3:
                        return LeapIndicator.AlarmNoSync;
                    default:
                        throw new ArgumentOutOfRangeException("NtpPacketHeader[0]", val, "Unknown leap indicator value.");
                }
            }
        }

        // Version Number (VN): This is a three-bit integer indicating the
        // NTP/SNTP version number, currently 4.  If necessary to distinguish
        // between IPv4, IPv6, and OSI, the encapsulating context must be
        // inspected.
        private byte VersionNumber
        {
            get
            {
                var octet00 = NtpPacketHeader[0];

                // Get bits 3 to 5: 0b_##_bbb_### --> 0b_00000_bbb
                var onMask = 0b_00_111_000;
                var rightShift = 3;
                var val = (octet00 & onMask) >> rightShift;

                return (byte)val;
            }
        }

        // Mode: This is a three-bit number indicating the protocol mode.  The
        // values are defined as follows:
        //
        //    Mode     Meaning
        //    ────────────────────────────────────
        //    0        reserved
        //    1        symmetric active
        //    2        symmetric passive
        //    3        client
        //    4        server
        //    5        broadcast
        //    6        reserved for NTP control message
        //    7        reserved for private use
        //
        // In unicast and manycast modes, the client sets this field to 3
        // (client) in the request, and the server sets it to 4 (server) in the
        // reply.  In broadcast mode, the server sets this field to 5
        // (broadcast).  The other modes are not used by SNTP servers and
        // clients.
        private Mode Mode
        {
            get
            {
                var octet00 = NtpPacketHeader[0];

                // Get three lowest bits: 0b_#####_bbb --> 0b_00000_bbb
                var onMask = 0b_00000_111;
                var val = octet00 & onMask;

                switch (val)
                {
                    case 0:
                        return Mode.Reserved;
                    case 1:
                        return Mode.SymmetricActive;
                    case 2:
                        return Mode.SymmetricPassive;
                    case 3:
                        return Mode.Client;
                    case 4:
                        return Mode.Server;
                    case 5:
                        return Mode.Broadcast;
                    case 6:
                        return Mode.Reserved;
                    case 7:
                        return Mode.Reserved;
                    default:
                        throw new ArgumentOutOfRangeException("NtpPacketHeader[0]", val, "Unknown protocol mode value.");
                }
            }
        }

        // Stratum: This is an eight-bit unsigned integer indicating the
        // stratum.  This field is significant only in SNTP server messages,
        // where the values are defined as follows:
        //
        //    Stratum  Meaning
        //    ──────────────────────────────────────────────
        //    0        kiss-o'-death message
        //    1        primary reference (e.g., synchronized by radio clock)
        //    2-15     secondary reference (synchronized by NTP or SNTP)
        //    16-255   reserved
        private Tuple<byte, Stratum> StratumCouple
        {
            get
            {
                Stratum stratumType;

                var stratumValue = NtpPacketHeader[1];
                if (stratumValue == 0)
                {
                    stratumType = Stratum.KissOfDeath;
                }
                else if (stratumValue == 1)
                {
                    stratumType = Stratum.Primary;
                }
                else if (stratumValue <= 15)
                {
                    stratumType = Stratum.Secondary;
                }
                else if (stratumValue <= 255)
                {
                    stratumType = Stratum.Reserved;
                }
                else
                {
                    throw new ArgumentOutOfRangeException("NtpPacketHeader[1]", stratumValue, "Unknown stratum value.");
                }
                return Tuple.Create(stratumValue, stratumType);
            }
        }

        // Poll Interval: This is an eight-bit unsigned integer used as an
        // exponent of two, where the resulting value is the maximum interval
        // between successive messages in seconds.  This field is significant
        // only in SNTP server messages, where the values range from 4 (16 s) to
        // 17 (131,072 s -- about 36 h).
        private Tuple<byte, uint> PollIntervalCouple
        {
            get
            {
                var pollIntervalValue = NtpPacketHeader[2];
                var pollIntervalSeconds = Math.Pow(2, pollIntervalValue);
                return Tuple.Create(pollIntervalValue, (uint)pollIntervalSeconds);
            }
        }

        // Precision: This is an eight-bit signed integer used as an exponent of
        // two, where the resulting value is the precision of the system clock
        // in seconds.  This field is significant only in server messages, where
        // the values range from -6 for mains-frequency clocks to -20 for
        // microsecond clocks found in some workstations.
        private Tuple<sbyte, double> PrecisionCouple
        {
            get
            {
                var precisionValue = (sbyte)NtpPacketHeader[3];
                var precisionSeconds = Math.Pow(2, precisionValue);
                return Tuple.Create(precisionValue, precisionSeconds * 1e9); // nanoseconds
            }
        }

        // Root Delay: This is a 32-bit unsigned [RFC 5905] fixed-point number
        // indicating the total roundtrip delay to the primary reference source,
        // in seconds with the fraction point between bits 15 and 16.
        private double RootDelay
        {
            get
            {
                var n = Offset.RootDelay;

                var val = 0.0;
                val += NtpPacketHeader[n + 0] * 256.0;   // rootDelayOctet[0] * 2^8
                val += NtpPacketHeader[n + 1] * 1.0;     // rootDelayOctet[1] * 2^0
                val += NtpPacketHeader[n + 2] / 256.0;   // rootDelayOctet[2] * 2^(-8)
                val += NtpPacketHeader[n + 3] / 65536.0; // rootDelayOctet[3] * 2^(-16)
                return val * 1e3; // milliseconds
            }
        }

        // Root Dispersion: This is a 32-bit unsigned fixed-point number
        // indicating the maximum error due to the clock frequency tolerance, in
        // seconds with the fraction point between bits 15 and 16.  This field
        // is significant only in server messages, where the values range from
        // zero to several hundred milliseconds.
        private double RootDispersion
        {
            get
            {
                var n = Offset.RootDispersion;

                var val = 0.0;
                val += NtpPacketHeader[n + 0] * 256.0;   // rootDispersionOctet[0] * 2^8
                val += NtpPacketHeader[n + 1] * 1.0;     // rootDispersionOctet[1] * 2^0
                val += NtpPacketHeader[n + 2] / 256.0;   // rootDispersionOctet[2] * 2^(-8)
                val += NtpPacketHeader[n + 3] / 65536.0; // rootDispersionOctet[3] * 2^(-16)
                return val * 1e3; // milliseconds
            }
        }

        // Reference Identifier: This is a 32-bit bitstring identifying the
        // particular reference source.  This field is significant only in
        // server messages, where for stratum 0 (kiss-o'-death message) and 1
        // (primary server), the value is a four-character ASCII string, left
        // justified and zero padded to 32 bits.  For IPv4 secondary servers,
        // the value is the 32-bit IPv4 address of the synchronization source.
        // For IPv6 and OSI secondary servers, the value is the first 32 bits of
        // the MD5 hash of the IPv6 or NSAP address of the synchronization
        // source.  (In NTP Version 3 secondary servers, this is the 32-bit IPv4
        // address of the reference source. <https://is.gd/SyB3vQ>)
        //
        // Primary (stratum 1) servers set this field to a code identifying the
        // external reference source.  If the external reference is one of those
        // listed, the associated code should be used.  Codes for sources not
        // listed can be contrived, as appropriate.
        //
        //    Code       External Reference Source
        //    ──────────────────────────────────────────────────────────────────
        //    LOCL       uncalibrated local clock
        //    CESM       calibrated Cesium clock
        //    RBDM       calibrated Rubidium clock
        //    PPS        calibrated quartz clock or other pulse-per-second
        //               source
        //    IRIG       Inter-Range Instrumentation Group
        //    ACTS       NIST telephone modem service
        //    USNO       USNO telephone modem service
        //    PTB        PTB (Germany) telephone modem service
        //    TDF        Allouis (France) Radio 164 kHz
        //    DCF        Mainflingen (Germany) Radio 77.5 kHz
        //    MSF        Anthorn (UK) Radio 60 kHz [Errata ID: 2480 <https://www.rfc-editor.org/errata/eid2480>]
        //    WWV        Ft. Collins (US) Radio 2.5, 5, 10, 15, 20 MHz
        //    WWVB       Boulder (US) Radio 60 kHz
        //    WWVH       Kauai Hawaii (US) Radio 2.5, 5, 10, 15 MHz
        //    CHU        Ottawa (Canada) Radio 3330, 7335, 14670 kHz
        //    LORC       LORAN-C radionavigation system
        //    OMEG       OMEGA radionavigation system
        //    GPS        Global Positioning Service
        //
        // If the Stratum field is 0, the Reference Identifier field can be used
        // to convey messages useful for status reporting and access control.
        // In NTPv4 and SNTPv4, packets of this kind are called Kiss-o'-Death
        // (KoD) packets, and the ASCII messages they convey are called kiss
        // codes:
        //
        //    Code    Meaning
        //    ──────────────────────────────────────────────────────────────
        //    ACST    The association belongs to a anycast server
        //    AUTH    Server authentication failed
        //    AUTO    Autokey sequence failed
        //    BCST    The association belongs to a broadcast server
        //    CRYP    Cryptographic authentication or identification failed
        //    DENY    Access denied by remote server
        //    DROP    Lost peer in symmetric mode
        //    RSTR    Access denied due to local policy
        //    INIT    The association has not yet synchronized for the first
        //            time
        //    MCST    The association belongs to a manycast server
        //    NKEY    No key found.  Either the key was never installed or
        //            is not trusted
        //    RATE    Rate exceeded.  The server has temporarily denied access
        //            because the client exceeded the rate threshold
        //    RMOT    Somebody is tinkering with the association from a remote
        //            host running ntpdc.  Not to worry unless some rascal has
        //            stolen your keys
        //    STEP    A step change in system time has occurred, but the
        //            association has not yet resynchronized
        private string ReferenceIdentifier
        {
            get
            {
                var n = Offset.ReferenceIdentifier;

                string val;
                switch (StratumCouple.Item2)
                {
                    case Stratum.KissOfDeath:
                    case Stratum.Primary:
                        // Reference ID is a four-character ASCII string
                        var asciiString = String.Format(
                            "{0}{1}{2}{3}",
                                (char)NtpPacketHeader[n + 0],
                                (char)NtpPacketHeader[n + 1],
                                (char)NtpPacketHeader[n + 2],
                                (char)NtpPacketHeader[n + 3]);
                        val = asciiString;
                        break;
                    case Stratum.Secondary:
                        switch (IpVersion)
                        {
                            case 4:
                                // Reference ID is the 32-bit IPv4 address of the synchronization source
                                var ipAddress = String.Format(
                                    "{0}.{1}.{2}.{3}",
                                        NtpPacketHeader[n + 0],
                                        NtpPacketHeader[n + 1],
                                        NtpPacketHeader[n + 2],
                                        NtpPacketHeader[n + 3]);
                                try
                                {
                                    // Resolve IP address to hostname within strict time limit (otherwise
                                    // Dns.GetHostEntry() can block until the operation is complete - approx.
                                    // 5 seconds). <https://stackoverflow.com/a/41353025>
//                     [equivalent] var getHostnameAsync = Dns.GetHostEntryAsync(ipAddress);
                                    var getHostnameAsync = Task.Factory.StartNew(() =>
                                    {
                                        try
                                        {
                                            var serverInfo = Dns.GetHostEntry(ipAddress);
                                            return serverInfo.HostName;
                                        }
                                        catch (Exception) // Domain not found
                                        {
                                            throw;
                                        }
                                    });

                                    // Wait for the task to complete execution within 'DnsTimeout'
                                    // milliseconds. 'isTaskComplete' is true if the task completed within
                                    // 'DnsTimeout' milliseconds; otherwise, false.
                                    var isTaskComplete = getHostnameAsync.Wait(DnsTimeout);

                                    if (isTaskComplete)
                                    {
                                        val = $"{getHostnameAsync.Result} ({ipAddress})";
                                    }
                                    else // Task timed out
                                    {
                                        val = ipAddress;
                                    }
                                }
                                catch (Exception)
                                {
                                    val = ipAddress;
                                }
                                break;
                            case 6:
                                // Reference ID is the first 32 bits of the MD5 hash of the IPv6 or NSAP
                                // address of the synchronization source.
                                var md5Hash32 = String.Format(
                                    "0x{0:X2}{1:X2}{2:X2}{3:X2}",
                                        NtpPacketHeader[n + 0],
                                        NtpPacketHeader[n + 1],
                                        NtpPacketHeader[n + 2],
                                        NtpPacketHeader[n + 3]);
                                val = md5Hash32;
                                break;
                            default:
                                val = "N/A";
                                break;
                        }
                        break;
                    case Stratum.Reserved:
                        val = "N/A";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("StratumCouple.Item2", StratumCouple.Item2, "Unknown stratum value.");
                }
                return val;
            }
        }

        // Reference Timestamp: This field is the time the system clock was last
        // set or corrected, in 64-bit timestamp format.
        //
        // [MGB: 28/09/2021] The documentation variously refers to this as the
        //   last correction time of the "local clock" (RFC 2030) or the "system
        //   clock" (RFC 4330), but observation strongly suggests it is actually
        //   the last time the reference clock (server) itself was corrected.
        private DateTime ReferenceTimestamp
        {
            get
            {
                return GetUtcFromTimestamp(Offset.ReferenceTimestamp);
            }
        }

        // Originate Timestamp: This is the time at which the request departed
        // the client for the server, in 64-bit timestamp format.
        private DateTime OriginateTimestamp
        {
            get
            {
                return GetUtcFromTimestamp(Offset.OriginateTimestamp);
            }
        }

        // Receive Timestamp: This is the time at which the request arrived at
        // the server or the reply arrived at the client, in 64-bit timestamp
        // format.
        private DateTime ReceiveTimestamp
        {
            get
            {
                return GetUtcFromTimestamp(Offset.ReceiveTimestamp);
            }
        }

        // Transmit Timestamp: This is the time at which the request departed
        // the client (set: T1) or the reply departed the server (get: T3), in
        // 64-bit timestamp format.
        private DateTime TransmitTimestamp
        {
            get
            {
                return GetUtcFromTimestamp(Offset.TransmitTimestamp);
            }
            set
            {
                DateTime utc = value;
                SetTimestamp(utc, Offset.TransmitTimestamp);
            }
        }

        // Roundtrip Delay and System Clock Offset <https://is.gd/LFl9pZ>
        // To calculate the roundtrip delay d and system clock offset t relative
        // to the server, the client sets the Transmit Timestamp field in the
        // request to the time of day according to the client clock in NTP
        // timestamp format.  For this purpose, the clock need not be
        // synchronized.  The server copies this field to the Originate
        // Timestamp in the reply and sets the Receive Timestamp and Transmit
        // Timestamp fields to the time of day according to the server clock in
        // NTP timestamp format.
        //
        // When the server reply is received, the client determines a
        // Destination Timestamp variable as the time of arrival according to
        // its clock in NTP timestamp format.  The following table summarizes
        // the four timestamps.
        //
        //    Timestamp Name          ID   When Generated
        //    ────────────────────────────────────────────────────────────
        //    Originate Timestamp     T1   time request sent by client
        //    Receive Timestamp       T2   time request received by server
        //    Transmit Timestamp      T3   time reply sent by server
        //    Destination Timestamp   T4   time reply received by client
        //
        // The roundtrip delay d and system clock offset t are defined as:
        //
        //    d = (T4 - T1) - (T3 - T2)     t = ((T2 - T1) + (T3 - T4)) / 2.
        //
        // Note that in general both delay and offset are signed quantities and
        // can be less than zero; however, a delay less than zero is possible
        // only in symmetric modes, which SNTP clients are forbidden to use.
        private DateTime DestinationTimestamp;

        // Roundtrip Delay (in milliseconds)
        private double RoundtripDelay
        {
            get
            {
                var T1 = OriginateTimestamp;
                var T2 = ReceiveTimestamp;
                var T3 = TransmitTimestamp;
                var T4 = DestinationTimestamp;
                var d = ((T4 - T1) - (T3 - T2)).TotalMilliseconds;
                return d;

                // No need for sanity checks, because SystemClockOffset is called first.
            }
        }

        // System Clock Offset (in milliseconds)
        private double SystemClockOffset
        {
            get
            {
                var T1 = OriginateTimestamp;
                var T2 = ReceiveTimestamp;
                var T3 = TransmitTimestamp;
                var T4 = DestinationTimestamp;
                var t = ((T2 - T1) + (T3 - T4)).TotalMilliseconds / 2.0;

                string s;
                // Sanity check: The arithmetic calculations used by NTP to determine
                // the clock offset and roundtrip delay require the client time to be
                // within 34 years of the server time before the client is launched.
/* BEGIN:DEBUG ****************************************************************/
// var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
// T1 = unixEpoch;
// T2 = unixEpoch;
// T3 = unixEpoch;
// T4 = unixEpoch;
/* END:DEBUG ******************************************************************/
                TimeSpan timeDisparity;
                timeDisparity = (T2 - T1).Duration();
                if (timeDisparity > MaxTimeDisparity)
                {
                    s = $"Time disparity between client and server is greater than {MaxYearsDisparity} years.\n";
                    s += $" Client: {T1:u}\n";
                    s += $" Server: {T2:u}";
                    throw new Exception(s);
                }

                timeDisparity = (T3 - T4).Duration();
                if (timeDisparity > MaxTimeDisparity)
                {
                    s = $"Time disparity between client and server is greater than {MaxYearsDisparity} years.\n";
                    s += $" Server: {T3:u}\n";
                    s += $" Client: {T4:u}";
                    throw new Exception(s);
                }

                // Sanity check: The server reply should be discarded if Transmit
                // Timestamp field is 0 (i.e., T3 == NtpEpoch or, more loosely,
                // (T3 - NtpEpoch).Duration() < 1 second).
/* BEGIN:DEBUG ****************************************************************/
// T3 = NtpEpoch;
// T3 = new DateTime(1899, 12, 31, 23, 59, 59, 500, DateTimeKind.Utc);
// T3 = new DateTime(1900,  1,  1,  0,  0,  0, 500, DateTimeKind.Utc);
// Console.ForegroundColor = ConsoleColor.Magenta;
// Console.WriteLine((T3 - NtpEpoch).TotalSeconds);
// Console.WriteLine((T3 - NtpEpoch).Duration().TotalSeconds);
// Console.ResetColor();
/* END:DEBUG ******************************************************************/
//              if (T3 == NtpEpoch)
                if ((T3 - NtpEpoch).Duration().TotalSeconds < 1)
                {
                    s = $"Invalid response from server <{ReferenceIdentifier}>: Server transmit timestamp field is 0 [T3 = 0].";
                    throw new Exception(s);
                }

                return t;
            }
        }

        // Convert a 64-bit NTP timestamp into UTC
        private DateTime GetUtcFromTimestamp(byte timestampOffset)
        {
            var n = timestampOffset;

//          // Seconds since NTP epoch (1900-01-01T00:00:00.0 UTC)
//          decimal deltaT = 0.0m;
//          // Integer part
//          deltaT += NtpPacketHeader[n + 0] * 16777216.0m;   // integerOctet[0] * 2^24 [256^3]
//          deltaT += NtpPacketHeader[n + 1] * 65536.0m;      // integerOctet[1] * 2^16 [256^2]
//          deltaT += NtpPacketHeader[n + 2] * 256.0m;        // integerOctet[2] * 2^8  [256^1]
//          deltaT += NtpPacketHeader[n + 3] * 1.0m;          // integerOctet[3] * 2^0  [256^0]
//          // Fractional part
//          deltaT += NtpPacketHeader[n + 4] / 256.0m;        // fractionalOctet[0] * 2^(-8)  [256^(-1)]
//          deltaT += NtpPacketHeader[n + 5] / 65536.0m;      // fractionalOctet[1] * 2^(-16) [256^(-2)]
//          deltaT += NtpPacketHeader[n + 6] / 16777216.0m;   // fractionalOctet[2] * 2^(-24) [256^(-3)]
//          deltaT += NtpPacketHeader[n + 7] / 4294967296.0m; // fractionalOctet[3] * 2^(-32) [256^(-4)]

            // Seconds since NTP epoch (1900-01-01T00:00:00.0 UTC)
            var deltaT = 0.0;
            // Integer part
            deltaT += NtpPacketHeader[n + 0] * 16777216.0;   // integerOctet[0] * 2^24 [256^3]
            deltaT += NtpPacketHeader[n + 1] * 65536.0;      // integerOctet[1] * 2^16 [256^2]
            deltaT += NtpPacketHeader[n + 2] * 256.0;        // integerOctet[2] * 2^8  [256^1]
            deltaT += NtpPacketHeader[n + 3] * 1.0;          // integerOctet[3] * 2^0  [256^0]
            // Fractional part
            deltaT += NtpPacketHeader[n + 4] / 256.0;        // fractionalOctet[0] * 2^(-8)  [256^(-1)]
            deltaT += NtpPacketHeader[n + 5] / 65536.0;      // fractionalOctet[1] * 2^(-16) [256^(-2)]
            deltaT += NtpPacketHeader[n + 6] / 16777216.0;   // fractionalOctet[2] * 2^(-24) [256^(-3)]
            deltaT += NtpPacketHeader[n + 7] / 4294967296.0; // fractionalOctet[3] * 2^(-32) [256^(-4)]

            // deltaT: double vs decimal
            // Using 'double' is less precise (approx. 5 ticks difference), but is
            // significantly faster (0.05 ms vs 3.5 ms).

            var totalTicks = (long)(deltaT * TimeSpan.TicksPerSecond);
            // Rounding to the nearest tick is technically more precise but is
            // computationally expensive.

//          var utc = NtpEpoch.AddSeconds(deltaT);   // less precise
            var utc = NtpEpoch.AddTicks(totalTicks);
            return utc;
        }

        // Set a 64-bit timestamp in the NTP packet header from UTC
        private void SetTimestamp(DateTime utc, byte timestampOffset)
        {
//          decimal deltaT = (utc - NtpEpoch).Ticks / (decimal)TimeSpan.TicksPerSecond; // high precision
            var deltaT = (utc - NtpEpoch).TotalSeconds;
            // Using ticks is technically more precise but is computationally
            // expensive.

            var integerPart = (uint)deltaT;
            var fractionalPart = deltaT - integerPart;

            var n = timestampOffset;

            uint onMask;
            byte rightShift;

            // Split integer part into four-octet timestamp (first half)
            onMask = 0b_11111111_00000000_00000000_00000000;
            rightShift = 24;
            NtpPacketHeader[n + 0] = (byte)((integerPart & onMask) >> rightShift);

            onMask = 0b_00000000_11111111_00000000_00000000;
            rightShift = 16;
            NtpPacketHeader[n + 1] = (byte)((integerPart & onMask) >> rightShift);

            onMask = 0b_00000000_00000000_11111111_00000000;
            rightShift = 8;
            NtpPacketHeader[n + 2] = (byte)((integerPart & onMask) >> rightShift);

            onMask = 0b_00000000_00000000_00000000_11111111;
            NtpPacketHeader[n + 3] = (byte)(integerPart & onMask);

            // Split fractional part into four-octet timestamp (second half)
            var leftShift08 = 256;        // 2^8  [256^1]
            NtpPacketHeader[n + 4] = (byte)(onMask & (uint)(fractionalPart * leftShift08));

            var leftShift16 = 65536;      // 2^16 [256^2]
            NtpPacketHeader[n + 5] = (byte)(onMask & (uint)(fractionalPart * leftShift16));

            var leftShift24 = 16777216;   // 2^24 [256^3]
            NtpPacketHeader[n + 6] = (byte)(onMask & (uint)(fractionalPart * leftShift24));

            var leftShift32 = 4294967296; // 2^32 [256^4]
            NtpPacketHeader[n + 7] = (byte)(onMask & (ulong)(fractionalPart * leftShift32));
        }

        // Connect to the time server and (optionally) update the system time
        public void ConnectToTimeServer(string hostnameOrIpAddress, bool shouldUpdateSystemTime)
        {
            try
            {
                // Preliminary sanity checks
                ValidateSystemClock();

                // Resolve server IP address
                IPAddress serverIpAddress;
                var isIpAddress = IPAddress.TryParse(hostnameOrIpAddress, out serverIpAddress);
                if (!isIpAddress)
                {
                    var serverInfo = Dns.GetHostEntry(hostnameOrIpAddress);
                    serverIpAddress = serverInfo.AddressList[0];
                }

                // Detect IP version <https://stackoverflow.com/a/799069>
                //   AddressFamily Enum <https://is.gd/Cv5JJW>
                switch (serverIpAddress.AddressFamily)
                {
                    case AddressFamily.InterNetwork:
                        IpVersion = 4;
                        break;
                    case AddressFamily.InterNetworkV6:
                        IpVersion = 6;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("serverIpAddress", serverIpAddress, "Unknown IP version.");
                }
/* BEGIN:DEBUG ****************************************************************/
// IpVersion = 6;
/* END:DEBUG ******************************************************************/

                // Connect to the time server
                var timeSocket = new UdpClient(serverIpAddress.AddressFamily);
                var ipEndPoint = new IPEndPoint(serverIpAddress, NtpPort);
                timeSocket.Connect(ipEndPoint);

                // Send/receive NTP message to/from the time server
                timeSocket.Client.SendTimeout = UdpTimeout;    // UdpClient send/receive timeout
                timeSocket.Client.ReceiveTimeout = UdpTimeout; // <https://stackoverflow.com/a/5684521>
                InitializeNtpPacketHeader();
                timeSocket.Send(NtpPacketHeader, NtpPacketHeader.Length);
                NtpPacketHeader = timeSocket.Receive(ref ipEndPoint);
                DestinationTimestamp = GetCurrentUtc();
                timeSocket.Close();
/* BEGIN:DEBUG ****************************************************************/
// stopwatch.Start();
// NtpPacketHeader[0] = 0b_11_100_100; // LeapIndicator
// NtpPacketHeader[0] = 0b_00_000_100; // VersionNumber
// NtpPacketHeader[0] = 0b_00_100_001; // Mode
// NtpPacketHeader[1] = 0; // Stratum
// TransmitTimestamp = NtpEpoch;
/* END:DEBUG ******************************************************************/
                ValidateServerResponse();
            }
            catch (SocketException e)
            {
                throw new Exception(e.Message);
            }
            catch (Exception)
            {
                throw;
            }

            // Update system time
            if (shouldUpdateSystemTime)
            {
                var t = SystemClockOffset;
                if (Math.Abs(t) > UpdateThreshold)
                {
//                  var newUtc = GetCorrectedUtc(TransmitTimestamp, DestinationTimestamp, RoundtripDelay);
                    var newUtc = GetCorrectedUtc(t);
                    SetSystemClock(newUtc);
                    // This must come before PrintData(), because the timeout from
                    // Dns.GetHostEntry(ipAddress) will add bias.
                }
                else
                {
                    IsTimeCorrected = false;
                    ErrorMessage = $"System clock offset within {UpdateThreshold} ms threshold";
                }
            }
            else
            {
                IsTimeCorrected = false;
            }

            PrintData();
            // This must come after SetSystemClock(), because the timeout from
            // Dns.GetHostEntry(ipAddress) will add bias.
        }
        private byte IpVersion;
        private bool IsTimeCorrected;
        private string ErrorMessage;

        // Check if the system time is valid
        private void ValidateSystemClock()
        {
            var utc = GetCurrentUtc();
/* BEGIN:DEBUG ****************************************************************/
// utc = new DateTime(1899, 12, 31, 23, 59, 59, 0, DateTimeKind.Utc);
// utc = new DateTime(2036, 2, 7, 6, 28, 16, 0, DateTimeKind.Utc);
/* END:DEBUG ******************************************************************/
            // System time should always be after NTP epoch
            if (utc < NtpEpoch)
            {
                throw new ArgumentOutOfRangeException("utc", utc, "Dates before NTP epoch (0h 0m 0s UTC on 1 January 1900) are invalid.");
            }

            // Seconds field overflows every 136 years
            //    Note that since some time in 1968 (second 2,147,483,648), the most
            //    significant bit (bit 0 of the integer part) has been set and that
            //    the 64-bit field will overflow some time in 2036 (second
            //    4,294,967,296).  There will exist a 232-picosecond interval,
            //    henceforth ignored, every 136 years when the 64-bit field will be
            //    0, which by convention is interpreted as an invalid or unavailable
            //    timestamp.
            var deltaT = (utc - NtpEpoch).TotalSeconds;
            if (deltaT > UInt32.MaxValue)
            {
                throw new ArgumentOutOfRangeException("deltaT", deltaT, $"Seconds field overflow (Max = {UInt32.MaxValue}).");
            }
        }

        // Initialize the NTP packet header
        private void InitializeNtpPacketHeader()
        {
            // Initialize:
            //   Leap Indicator = 0
            //   Version Number = 4
            //   Mode           = 3 (client)
            NtpPacketHeader[0] = 0b_00_100_011; // LI = 0, VN = 4, Mode = 3

            // Initialize all other fields with 0
            for (var i = 1; i < 48; i++)
            {
                NtpPacketHeader[i] = 0;
            }

            // Initialize the transmit timestamp
            TransmitTimestamp = GetCurrentUtc();
        }

        private DateTime GetCurrentUtc() => DateTime.UtcNow;

        // Check if the response from server is valid (sanity checks)
        //    The server reply should be discarded if any of the VN, Stratum,
        //    or Transmit Timestamp fields is 0 or the Mode field is not 4
        //    (server [unicast]) or 5 (broadcast). [Errata ID: 2263
        //    <https://www.rfc-editor.org/errata/eid2263>]
        private void ValidateServerResponse()
        {
            string s;
            // Must have call to ReferenceIdentifier inside `if` block to avoid
            // adding unnecessary delay.

            // NTP packet header is missing data
            if (NtpPacketHeader.Length < NtpPacketHeaderLength)
            {
                s = $"Invalid response from server <{ReferenceIdentifier}>: NTP packet header is missing data.\n";
                s += $" Expected: {NtpPacketHeaderLength} octets\n";
                s += $" Received: {NtpPacketHeader.Length} octets";
                throw new Exception(s);
            }

            // Leap Indicator field is 3 (clock not synchronized)
            if (LeapIndicator == LeapIndicator.AlarmNoSync)
            {
                s = $"Invalid response from server <{ReferenceIdentifier}>: Reference clock never synchronized [LI = {LeapIndicator}].";
                throw new Exception(s);
            }

            // Version Number field is 0
            if (VersionNumber == 0)
            {
                s = $"Invalid response from server <{ReferenceIdentifier}>: (S)NTP version number is 0 [VN = {VersionNumber}].";
                throw new Exception(s);
            }

            // Mode field is not 4 (server [unicast]) or 5 (broadcast)
            var mode = Mode;
            if ((mode != Mode.Server) && (mode != Mode.Broadcast))
            {
                s = $"Invalid response from server <{ReferenceIdentifier}>: Protocol mode: {mode}";
                throw new Exception(s);
            }

//          // Stratum field is 0
//          if (StratumCouple.Item1 == 0)
//          {
//              s = $"Invalid response from server <{ReferenceIdentifier}>: Stratum: {StratumCouple.Item1} [{StratumCouple.Item2}]";
//              throw new Exception(s);
//          }

// CODE BLOCK MOVED TO SystemClockOffset (faster)
//          // Transmit Timestamp field is 0
// //       if (TransmitTimestamp == NtpEpoch)
//          if ((TransmitTimestamp - NtpEpoch).TotalSeconds < 1)
//          {
//              s = $"Invalid response from server <{ReferenceIdentifier}>: Server transmit timestamp field is 0 [T3 = 0].";
//              throw new Exception(s);
//          }
        }

        // UPDATE METHODS
        // Method 1: Update using the Transmit Timestamp with corrections for
        //           network delay and delay introduced by intermediate program
        //           instructions.
        // Method 2: Update using just the system clock offset 't' to correct
        //           the system clock.
        //
        // Comparison of system clock offset 't' (20 samples):
        //
        //                ┌─────────────┬─────────────┐
        //                │   Method 1  │   Method 2  │
        //     ┌──────────┼─────────────┼─────────────┤
        //     │  median  │   5.009 ms  │   3.610 ms  │
        //     │   mean   │   4.321 ms  │   5.098 ms  │
        //     │    std   │   9.949 ms  │   7.006 ms  │
        //     └──────────┴─────────────┴─────────────┘
        //
        // Mean Stopwatch() time (20 samples):
        //
        //      Method 1:  1.3483 ms
        //      Method 2:  0.5005 ms  <-- faster
        //
        // Both methods have similar accuracy, but Method 2 is faster.
        private DateTime GetCorrectedUtc(DateTime transmitTimestamp, DateTime destinationTimestamp, double roundtripDelay)
        {
            // METHOD 1: Transmit Timestamp with corrections

            // Compensate for trip delay, server --> client ("travelling time")
//          var tripDelay = TimeSpan.FromMilliseconds(roundtripDelay / 2.0);
            // Using TimeSpan.FromMilliseconds() directly is less precise, because
            // accuracy is only to the nearest millisecond.

            var halfRoundtripDelay = (long)(roundtripDelay / 2.0 * TimeSpan.TicksPerMillisecond);
            var tripDelay = TimeSpan.FromTicks(halfRoundtripDelay);

            // Compensate for bias introduced by intermediate code:
            //   ValidateServerResponse()      ~6.0 ms
            //   Math.Abs(SystemClockOffset)   ~8.5 ms
            //   halfRoundtripDelay            ~1.0 ms
            var codeDelay = GetCurrentUtc() - destinationTimestamp;

//          var newUtc = transmitTimestamp.AddTicks(halfRoundtripDelay) + codeDelay; // equivalent
            var newUtc = transmitTimestamp + tripDelay + codeDelay;
            return newUtc;
        }

        private DateTime GetCorrectedUtc(double systemClockOffset)
        {
            // METHOD 2: Correct with system clock offset 't'

//          var clockOffset = TimeSpan.FromMilliseconds(systemClockOffset);
            // Using TimeSpan.FromMilliseconds() or DateTime.AddMilliseconds()
            // directly is less precise, because accuracy is only to the nearest
            // millisecond.

            var ticksOffset = (long)(systemClockOffset * TimeSpan.TicksPerMillisecond);
            var clockOffset = TimeSpan.FromTicks(ticksOffset);

//          var newUtc = GetCurrentUtc().AddMilliseconds(systemClockOffset); // less precise
//          var newUtc = GetCurrentUtc().AddTicks(ticksOffset); // equivalent
            var newUtc = GetCurrentUtc() + clockOffset;
            return newUtc;
        }

//      [DllImport("kernel32.dll", SetLastError = true)] // SetLastError <https://stackoverflow.com/a/35502151>
//      private static extern bool SetLocalTime(ref SYSTEMTIME time);
        // The system uses UTC internally. Therefore, when you call
        // SetLocalTime(), the system uses the current time zone information to
        // perform the conversion, including the daylight saving time setting.
        // Note that the system uses the daylight saving time setting of the
        // current time, not the new time you are setting. Therefore, to ensure
        // the correct result, call SetLocalTime() a second time, now that the
        // first call has updated the daylight saving time setting.
        // <https://is.gd/dVaxQ0>

        [DllImport("kernel32.dll", SetLastError = true)] // SetLastError <https://stackoverflow.com/a/35502151>
        private static extern bool SetSystemTime(ref SYSTEMTIME time);

        // Set/adjust the system time
        //   <https://stackoverflow.com/a/6083225>
        //   <https://stackoverflow.com/a/650872>
        private void SetSystemClock(DateTime newTime)
        {
            SYSTEMTIME time = new SYSTEMTIME();

            time.wYear = (ushort)newTime.Year;
            time.wMonth = (ushort)newTime.Month;
            time.wDay = (ushort)newTime.Day;
            time.wHour = (ushort)newTime.Hour;
            time.wMinute = (ushort)newTime.Minute;
            time.wSecond = (ushort)newTime.Second;
//          time.wMilliseconds = (ushort)newTime.Millisecond; // truncates fraction
            time.wMilliseconds = (ushort)newTime.AddTicks(TruncationCorrection).Millisecond;

            IsTimeCorrected = SetSystemTime(ref time); // Execution time: ~3 ms

            var errorValue = Marshal.GetLastWin32Error();          // NET HELPMSG 1314
            ErrorMessage = new Win32Exception(errorValue).Message; // <https://stackoverflow.com/a/1650868>
        }

        // Display the data received from the time server
        private void PrintData()
        {
            Console.Write(" Leap Indicator: ");
            switch (LeapIndicator)
            {
                case LeapIndicator.NoWarning:
                    Console.WriteLine("0 (no warning)");
                    break;
                case LeapIndicator.LastMinute61:
                    Console.WriteLine("1 (last minute has 61 seconds)");
                    break;
                case LeapIndicator.LastMinute59:
                    Console.WriteLine("2 (last minute has 59 seconds)");
                    break;
                case LeapIndicator.AlarmNoSync:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("3 (alarm condition - clock not synchronized)");
                    Console.ResetColor();
                    break;
                default:
                    throw new ArgumentOutOfRangeException("LeapIndicator", LeapIndicator, "Unknown leap indicator value.");
            }

            Console.WriteLine($" NTP Version: {VersionNumber}");

//          Console.Write(" Mode: ");
//          switch (Mode)
//          {
//              case Mode.Reserved:
//                  Console.WriteLine("Reserved");
//                  break;
//              case Mode.SymmetricActive:
//                  Console.WriteLine("Symmetric Active");
//                  break;
//              case Mode.SymmetricPassive:
//                  Console.WriteLine("Symmetric Pasive");
//                  break;
//              case Mode.Client:
//                  Console.WriteLine("Client");
//                  break;
//              case Mode.Server:
//                  Console.WriteLine("Server");
//                  break;
//              case Mode.Broadcast:
//                  Console.WriteLine("Broadcast");
//                  break;
//              default:
//                  throw new ArgumentOutOfRangeException("Mode", Mode, "Unknown protocol mode value.");
//          }

            var stratumCouple = StratumCouple;
            Console.Write(" Stratum: ");
            switch (stratumCouple.Item2)
            {
                case Stratum.KissOfDeath:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("0 (kiss-o'-death message)");
                    Console.ResetColor();
                    break;
                case Stratum.Primary:
                    Console.WriteLine("1 (primary reference - synchronized by reference clock)");
                    break;
                case Stratum.Secondary:
                    Console.WriteLine($"{stratumCouple.Item1} (secondary reference - synchronized by NTP or SNTP)");
                    break;
                case Stratum.Reserved:
                    Console.WriteLine($"{stratumCouple.Item1} (reserved)");
                    break;
                default:
                    throw new ArgumentOutOfRangeException("stratumCouple.Item2", stratumCouple.Item2, "Unknown stratum value.");
            }

            var pollIntervalCouple = PollIntervalCouple;
            Console.WriteLine($" Poll Interval: {pollIntervalCouple.Item1} ({pollIntervalCouple.Item2} s)");

            var precisionCouple = PrecisionCouple;
            Console.WriteLine($" Precision: {precisionCouple.Item1} ({precisionCouple.Item2:G5} ns)");

            Console.WriteLine($" Root Delay: {RootDelay:G5} ms");
            Console.WriteLine($" Root Dispersion: {RootDispersion:G5} ms");

            if (stratumCouple.Item2 == Stratum.KissOfDeath)
            {
                Console.Write(" Reference ID: ");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ReferenceIdentifier);
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($" Reference ID: {ReferenceIdentifier}");
            }

//          Console.WriteLine(" Local Time: {0:G}{0:%K}", OriginateTimestamp.ToLocalTime());
            Console.WriteLine(" Local Time: {0:yyyy/MM/dd HH:mm:ss.FFFK}", OriginateTimestamp.ToLocalTime());

            var d = RoundtripDelay;
            Console.Write(" Round Trip Delay: ");
            if (Math.Abs(d) < 1000)
            {
                Console.WriteLine("{0:F2} ms", d);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("{0:G5} ms", d);
                Console.ResetColor();
            }

            var t = SystemClockOffset;
            Console.Write(" System Clock Offset: ");
            if (Math.Abs(t) < 100)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("{0:F2} ms", t);
            }
            else if (Math.Abs(t) < 1000)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("{0:F2} ms", t);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("{0:G5} ms", t);
            }
            Console.ResetColor();
            Console.WriteLine();

            if (IsTimeCorrected)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("SYSTEM TIME UPDATED");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                if (String.IsNullOrEmpty(ErrorMessage))
                {
                    Console.WriteLine("SYSTEM TIME NOT UPDATED");
                }
                else
                {
                    Console.WriteLine($"SYSTEM TIME NOT UPDATED - {ErrorMessage}");
                }
            }
            Console.ResetColor();

/* BEGIN:DEBUG ****************************************************************/
// Console.WriteLine();
// Console.ForegroundColor = ConsoleColor.Green;
// Console.WriteLine("NtpEpoch:              {0:O}", NtpEpoch);
// Console.ResetColor();
// Console.ForegroundColor = ConsoleColor.Magenta;
// Console.WriteLine("Reference Timestamp:   {0:O}", ReferenceTimestamp);
// Console.ResetColor();
// Console.WriteLine("Originate Timestamp:   {0:O}", OriginateTimestamp);
// Console.WriteLine("Receive Timestamp:     {0:O}", ReceiveTimestamp);
// Console.WriteLine("Transmit Timestamp:    {0:O}", TransmitTimestamp);
// Console.WriteLine("Destination Timestamp: {0:O}", DestinationTimestamp);
// Console.WriteLine();
/* END:DEBUG ******************************************************************/
        }
    }
}
