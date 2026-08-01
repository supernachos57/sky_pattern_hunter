using System.Globalization;

namespace SkyPatternHunter.Infrastructure.AdsB;

public sealed class RawAdsbFrameDecoder
{
    private const int CprScale = 131072;
    private readonly Dictionary<string, RawAircraftState> _states = new(StringComparer.OrdinalIgnoreCase);

    public RawAdsbDecodeResult Decode(string normalizedHex, DateTimeOffset observedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedHex);

        if (!TryParseHex(normalizedHex, out var bytes) || bytes.Length != 14)
        {
            return RawAdsbDecodeResult.Fallback(normalizedHex);
        }

        var downlinkFormat = bytes[0] >> 3;
        if (downlinkFormat is not 17 and not 18)
        {
            return RawAdsbDecodeResult.Fallback(normalizedHex);
        }

        var icao = $"{bytes[1]:X2}{bytes[2]:X2}{bytes[3]:X2}";
        if (!_states.TryGetValue(icao, out var state))
        {
            state = new RawAircraftState(icao);
            _states[icao] = state;
        }

        var me = ReadMessageExtension(bytes);
        var typeCode = GetBits(me, 1, 5);

        if (typeCode is >= 1 and <= 4)
        {
            var callsign = DecodeCallsign(me);
            if (!string.IsNullOrWhiteSpace(callsign))
            {
                state.Flight = callsign;
            }
        }
        else if ((typeCode is >= 9 and <= 18) || (typeCode is >= 20 and <= 22))
        {
            var altitudeCode = GetBits(me, 9, 12);
            var decodedAltitude = DecodeAltitude(altitudeCode);
            if (decodedAltitude.HasValue)
            {
                state.Altitude = decodedAltitude.Value;
            }

            var isOdd = GetBits(me, 22, 1) == 1;
            var latCpr = GetBits(me, 23, 17);
            var lonCpr = GetBits(me, 40, 17);
            var cprFrame = new CprFrame(isOdd, latCpr, lonCpr, observedAt);

            if (isOdd)
            {
                state.OddFrame = cprFrame;
            }
            else
            {
                state.EvenFrame = cprFrame;
            }

            if (TryDecodeCprPosition(state, out var latitude, out var longitude))
            {
                state.Latitude = latitude;
                state.Longitude = longitude;
            }
        }
        else if (typeCode == 19)
        {
            var subtype = GetBits(me, 6, 3);
            if (subtype is 1 or 2)
            {
                var eastWestDirection = GetBits(me, 14, 1);
                var eastWestRaw = GetBits(me, 15, 10);
                var northSouthDirection = GetBits(me, 25, 1);
                var northSouthRaw = GetBits(me, 26, 10);

                if (eastWestRaw > 0 && northSouthRaw > 0)
                {
                    var unitScale = subtype == 2 ? 4 : 1;
                    var eastWestVelocity = (eastWestRaw - 1) * unitScale * (eastWestDirection == 1 ? -1 : 1);
                    var northSouthVelocity = (northSouthRaw - 1) * unitScale * (northSouthDirection == 1 ? -1 : 1);

                    var speed = (int)Math.Round(Math.Sqrt((eastWestVelocity * eastWestVelocity) + (northSouthVelocity * northSouthVelocity)));
                    var track = (int)Math.Round((Math.Atan2(eastWestVelocity, northSouthVelocity) * 180.0 / Math.PI + 360.0) % 360.0);

                    state.Speed = speed;
                    state.Track = track;
                }
            }
        }

        return new RawAdsbDecodeResult(
            state.Hex,
            state.Flight,
            state.Latitude,
            state.Longitude,
            state.Altitude,
            state.Track,
            state.Speed,
            state.Squawk);
    }

    private static ulong ReadMessageExtension(byte[] bytes)
    {
        ulong value = 0;
        for (var index = 4; index <= 10; index++)
        {
            value = (value << 8) | bytes[index];
        }

        return value;
    }

    private static int GetBits(ulong value, int startBit, int bitCount)
    {
        var shift = 56 - (startBit + bitCount - 1);
        var mask = (1UL << bitCount) - 1;
        return (int)((value >> shift) & mask);
    }

    private static string DecodeCallsign(ulong messageExtension)
    {
        Span<char> callsign = stackalloc char[8];
        for (var index = 0; index < 8; index++)
        {
            var characterCode = GetBits(messageExtension, 9 + (index * 6), 6);
            callsign[index] = DecodeCallsignCharacter(characterCode);
        }

        return new string(callsign).Trim();
    }

    private static char DecodeCallsignCharacter(int code)
    {
        return code switch
        {
            1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21 or 22 or 23 or 24 or 25 or 26
                => (char)('A' + (code - 1)),
            48 or 49 or 50 or 51 or 52 or 53 or 54 or 55 or 56 or 57 => (char)('0' + (code - 48)),
            _ => ' '
        };
    }

    private static int? DecodeAltitude(int altitudeCode)
    {
        // ADS-B airborne position altitude with Q bit set is encoded in 25 ft increments.
        if ((altitudeCode & 0b0001_0000) == 0)
        {
            return null;
        }

        var n = ((altitudeCode & 0b1111_1110_0000) >> 1) | (altitudeCode & 0b0000_0000_1111);
        return (n * 25) - 1000;
    }

    private static bool TryDecodeCprPosition(RawAircraftState state, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;

        if (state.EvenFrame is null || state.OddFrame is null)
        {
            return false;
        }

        var even = state.EvenFrame;
        var odd = state.OddFrame;
        if (Math.Abs((even.Timestamp - odd.Timestamp).TotalSeconds) > 10)
        {
            return false;
        }

        var evenLat = even.LatitudeCpr;
        var oddLat = odd.LatitudeCpr;
        var evenLon = even.LongitudeCpr;
        var oddLon = odd.LongitudeCpr;

        const double dLatEven = 360.0 / 60.0;
        const double dLatOdd = 360.0 / 59.0;

        var latitudeIndex = (int)Math.Floor(((59.0 * evenLat) - (60.0 * oddLat)) / CprScale + 0.5);
        var decodedEvenLatitude = dLatEven * (PositiveModulo(latitudeIndex, 60) + evenLat / (double)CprScale);
        var decodedOddLatitude = dLatOdd * (PositiveModulo(latitudeIndex, 59) + oddLat / (double)CprScale);

        if (decodedEvenLatitude >= 270)
        {
            decodedEvenLatitude -= 360;
        }

        if (decodedOddLatitude >= 270)
        {
            decodedOddLatitude -= 360;
        }

        var useOdd = odd.Timestamp >= even.Timestamp;
        var selectedLatitude = useOdd ? decodedOddLatitude : decodedEvenLatitude;

        var nl = CalculateLongitudeZoneCount(selectedLatitude);
        if (nl == 0)
        {
            return false;
        }

        var ni = useOdd ? nl - 1 : nl;
        if (ni <= 0)
        {
            ni = 1;
        }

        var longitudeIndex = (int)Math.Floor(((evenLon * (nl - 1.0)) - (oddLon * nl)) / CprScale + 0.5);
        var longitudeSpacing = 360.0 / ni;
        var selectedLongitudeCpr = useOdd ? oddLon : evenLon;
        var decodedLongitude = longitudeSpacing * (PositiveModulo(longitudeIndex, ni) + selectedLongitudeCpr / (double)CprScale);

        if (decodedLongitude > 180)
        {
            decodedLongitude -= 360;
        }

        latitude = selectedLatitude;
        longitude = decodedLongitude;
        return true;
    }

    private static int PositiveModulo(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    // NL table approximation from ICAO CPR specification.
    private static int CalculateLongitudeZoneCount(double latitude)
    {
        var absoluteLatitude = Math.Abs(latitude);

        if (absoluteLatitude < 10.47047130) return 59;
        if (absoluteLatitude < 14.82817437) return 58;
        if (absoluteLatitude < 18.18626357) return 57;
        if (absoluteLatitude < 21.02939493) return 56;
        if (absoluteLatitude < 23.54504487) return 55;
        if (absoluteLatitude < 25.82924707) return 54;
        if (absoluteLatitude < 27.93898710) return 53;
        if (absoluteLatitude < 29.91135686) return 52;
        if (absoluteLatitude < 31.77209708) return 51;
        if (absoluteLatitude < 33.53993436) return 50;
        if (absoluteLatitude < 35.22899598) return 49;
        if (absoluteLatitude < 36.85025108) return 48;
        if (absoluteLatitude < 38.41241892) return 47;
        if (absoluteLatitude < 39.92256684) return 46;
        if (absoluteLatitude < 41.38651832) return 45;
        if (absoluteLatitude < 42.80914012) return 44;
        if (absoluteLatitude < 44.19454951) return 43;
        if (absoluteLatitude < 45.54626723) return 42;
        if (absoluteLatitude < 46.86733252) return 41;
        if (absoluteLatitude < 48.16039128) return 40;
        if (absoluteLatitude < 49.42776439) return 39;
        if (absoluteLatitude < 50.67150166) return 38;
        if (absoluteLatitude < 51.89342469) return 37;
        if (absoluteLatitude < 53.09516153) return 36;
        if (absoluteLatitude < 54.27817472) return 35;
        if (absoluteLatitude < 55.44378444) return 34;
        if (absoluteLatitude < 56.59318756) return 33;
        if (absoluteLatitude < 57.72747354) return 32;
        if (absoluteLatitude < 58.84763776) return 31;
        if (absoluteLatitude < 59.95459277) return 30;
        if (absoluteLatitude < 61.04917774) return 29;
        if (absoluteLatitude < 62.13216659) return 28;
        if (absoluteLatitude < 63.20427479) return 27;
        if (absoluteLatitude < 64.26616523) return 26;
        if (absoluteLatitude < 65.31845310) return 25;
        if (absoluteLatitude < 66.36171008) return 24;
        if (absoluteLatitude < 67.39646774) return 23;
        if (absoluteLatitude < 68.42322022) return 22;
        if (absoluteLatitude < 69.44242631) return 21;
        if (absoluteLatitude < 70.45451075) return 20;
        if (absoluteLatitude < 71.45986473) return 19;
        if (absoluteLatitude < 72.45884545) return 18;
        if (absoluteLatitude < 73.45177442) return 17;
        if (absoluteLatitude < 74.43893416) return 16;
        if (absoluteLatitude < 75.42056257) return 15;
        if (absoluteLatitude < 76.39684391) return 14;
        if (absoluteLatitude < 77.36789461) return 13;
        if (absoluteLatitude < 78.33374083) return 12;
        if (absoluteLatitude < 79.29428225) return 11;
        if (absoluteLatitude < 80.24923213) return 10;
        if (absoluteLatitude < 81.19801349) return 9;
        if (absoluteLatitude < 82.13956981) return 8;
        if (absoluteLatitude < 83.07199445) return 7;
        if (absoluteLatitude < 83.99173563) return 6;
        if (absoluteLatitude < 84.89166191) return 5;
        if (absoluteLatitude < 85.75541621) return 4;
        if (absoluteLatitude < 86.53536998) return 3;
        if (absoluteLatitude < 87.00000000) return 2;
        return 1;
    }

    private static bool TryParseHex(string hex, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if ((hex.Length % 2) != 0)
        {
            return false;
        }

        var parsedBytes = new byte[hex.Length / 2];
        for (var index = 0; index < parsedBytes.Length; index++)
        {
            var offset = index * 2;
            if (!byte.TryParse(hex.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsedBytes[index]))
            {
                return false;
            }
        }

        bytes = parsedBytes;
        return true;
    }

    private sealed class RawAircraftState
    {
        public RawAircraftState(string hex)
        {
            Hex = hex;
        }

        public string Hex { get; }
        public string? Flight { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int? Altitude { get; set; }
        public int? Track { get; set; }
        public int? Speed { get; set; }
        public int? Squawk { get; set; }
        public CprFrame? EvenFrame { get; set; }
        public CprFrame? OddFrame { get; set; }
    }

    private sealed record CprFrame(bool IsOdd, int LatitudeCpr, int LongitudeCpr, DateTimeOffset Timestamp);
}

public sealed record RawAdsbDecodeResult(
    string Hex,
    string? Flight,
    double? Latitude,
    double? Longitude,
    int? Altitude,
    int? Track,
    int? Speed,
    int? Squawk)
{
    public static RawAdsbDecodeResult Fallback(string normalizedHex)
    {
        return new RawAdsbDecodeResult(normalizedHex, null, null, null, null, null, null, null);
    }
}
