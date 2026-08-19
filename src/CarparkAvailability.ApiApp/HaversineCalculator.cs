namespace CarparkAvailability.ApiApp;

public static class HaversineCalculator
{
    private const double EarthRadiusMetres = 6_371_000;

    public static double DistanceMetres(double lat1, double lon1, double lat2, double lon2)
    {
        var latitudeDelta = DegreesToRadians(lat2 - lat1);
        var longitudeDelta = DegreesToRadians(lon2 - lon1);
        var startLatitude = DegreesToRadians(lat1);
        var endLatitude = DegreesToRadians(lat2);

        var a = Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2)
            + Math.Cos(startLatitude) * Math.Cos(endLatitude)
            * Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusMetres * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
