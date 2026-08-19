namespace CarparkAvailability.ApiApp;

public static class Svy21Converter
{
    private const double SemiMajorAxis = 6_378_137.0;
    private const double InverseFlattening = 298.257223563;
    private const double FalseEasting = 28_001.642;
    private const double FalseNorthing = 38_744.572;
    private const double LatitudeOfOrigin = 1.3666666666666667 * Math.PI / 180.0;
    private const double LongitudeOfOrigin = 103.83333333333333 * Math.PI / 180.0;
    private const double ScaleFactor = 1.0;

    private static readonly double Flattening = 1.0 / InverseFlattening;
    private static readonly double SemiMinorAxis = SemiMajorAxis * (1 - Flattening);
    private static readonly double EccentricitySquared = 2 * Flattening - Flattening * Flattening;
    private static readonly double A0 = 1 - EccentricitySquared / 4 - 3 * Math.Pow(EccentricitySquared, 2) / 64 - 5 * Math.Pow(EccentricitySquared, 3) / 256;
    private static readonly double A2 = 3.0 / 8.0 * (EccentricitySquared + Math.Pow(EccentricitySquared, 2) / 4 + 15 * Math.Pow(EccentricitySquared, 3) / 128);
    private static readonly double A4 = 15.0 / 256.0 * (Math.Pow(EccentricitySquared, 2) + 3 * Math.Pow(EccentricitySquared, 3) / 4);
    private static readonly double A6 = 35 * Math.Pow(EccentricitySquared, 3) / 3072.0;
    private static readonly double MeridionalArcAtOrigin = CalculateMeridionalArc(LatitudeOfOrigin);

    public static (double Latitude, double Longitude) Convert(double easting, double northing)
    {
        if (double.IsNaN(easting) || double.IsNaN(northing) || double.IsInfinity(easting) || double.IsInfinity(northing))
        {
            throw new ArgumentOutOfRangeException(nameof(easting), "Coordinates must be finite values.");
        }

        var meridionalArc = MeridionalArcAtOrigin + (northing - FalseNorthing) / ScaleFactor;
        var footpointLatitude = SolveFootpointLatitude(meridionalArc);

        var sinFootpoint = Math.Sin(footpointLatitude);
        var cosFootpoint = Math.Cos(footpointLatitude);
        var tanFootpoint = Math.Tan(footpointLatitude);
        var transverseRadius = CalculateNu(sinFootpoint);
        var meridianRadius = CalculateRho(sinFootpoint);
        var psi = transverseRadius / meridianRadius;
        var eastingOffset = easting - FalseEasting;
        var x = eastingOffset / (ScaleFactor * transverseRadius);
        var x2 = x * x;
        var x3 = x2 * x;
        var x4 = x2 * x2;
        var x5 = x4 * x;
        var x6 = x3 * x3;
        var x7 = x6 * x;
        var x8 = x4 * x4;
        var tan2 = tanFootpoint * tanFootpoint;
        var tan4 = tan2 * tan2;
        var tan6 = tan4 * tan2;
        var psi2 = psi * psi;
        var psi3 = psi2 * psi;
        var psi4 = psi2 * psi2;

        var latitude = footpointLatitude
            - tanFootpoint / (2 * meridianRadius) * eastingOffset * x
            + tanFootpoint / (24 * meridianRadius) * eastingOffset * x3 * (-4 * psi2 + 9 * psi * (1 - tan2) + 12 * tan2)
            - tanFootpoint / (720 * meridianRadius) * eastingOffset * x5 * (8 * psi4 * (11 - 24 * tan2) - 12 * psi3 * (21 - 71 * tan2) + 15 * psi2 * (15 - 98 * tan2 + 15 * tan4) + 180 * psi * (5 * tan2 - 3 * tan4) + 360 * tan4)
            + tanFootpoint / (40320 * meridianRadius) * eastingOffset * x7 * (1385 + 3633 * tan2 + 4095 * tan4 + 1575 * tan6);

        var longitude = LongitudeOfOrigin
            + x / cosFootpoint
            - x3 / (6 * cosFootpoint) * (psi + 2 * tan2)
            + x5 / (120 * cosFootpoint) * (-4 * psi3 * (1 - 6 * tan2) + psi2 * (9 - 68 * tan2) + 72 * psi * tan2 + 24 * tan4)
            - x7 / (5040 * cosFootpoint) * (61 + 662 * tan2 + 1320 * tan4 + 720 * tan6);

        return (latitude * 180.0 / Math.PI, longitude * 180.0 / Math.PI);
    }

    private static double CalculateMeridionalArc(double latitude)
    {
        return SemiMajorAxis *
            ((A0 * latitude)
            - (A2 * Math.Sin(2 * latitude))
            + (A4 * Math.Sin(4 * latitude))
            - (A6 * Math.Sin(6 * latitude)));
    }

    private static double SolveFootpointLatitude(double meridionalArc)
    {
        var latitude = meridionalArc / (SemiMajorAxis * A0);

        for (var iteration = 0; iteration < 8; iteration++)
        {
            var delta = (meridionalArc - CalculateMeridionalArc(latitude)) / (SemiMajorAxis * A0);
            latitude += delta;

            if (Math.Abs(delta) < 1e-12)
            {
                break;
            }
        }

        return latitude;
    }

    private static double CalculateNu(double sinLatitude)
    {
        return SemiMajorAxis / Math.Sqrt(1 - EccentricitySquared * sinLatitude * sinLatitude);
    }

    private static double CalculateRho(double sinLatitude)
    {
        return SemiMajorAxis * (1 - EccentricitySquared) / Math.Pow(1 - EccentricitySquared * sinLatitude * sinLatitude, 1.5);
    }
}
