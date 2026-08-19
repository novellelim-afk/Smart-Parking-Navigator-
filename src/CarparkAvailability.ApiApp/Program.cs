using CarparkAvailability.ApiApp.Services;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient("datagov", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["DataGovSg:BaseUrl"] ?? "https://api.data.gov.sg/v1/");
});
builder.Services.AddSingleton<CsvIngestionService>();
builder.Services.AddSingleton<AvailabilityStore>();
builder.Services.AddSingleton<CarparkService>();
builder.Services.AddHostedService<DataGovSgPollingService>();

var app = builder.Build();

_ = app.Services.GetRequiredService<CsvIngestionService>();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/api", () => Results.Ok(new
{
    name = "Smart Parking Navigator API",
    status = "Starter scaffold"
}));

app.MapGet("/api/carparks", (HttpContext httpContext, CarparkService carparkService) =>
{
    var latQuery = httpContext.Request.Query["lat"].ToString();
    var lngQuery = httpContext.Request.Query["lng"].ToString();
    var radiusQuery = httpContext.Request.Query["radius"].ToString();

    if (string.IsNullOrWhiteSpace(latQuery) || string.IsNullOrWhiteSpace(lngQuery))
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Missing coordinates", detail: "Both lat and lng query parameters are required.");
    }

    if (!double.TryParse(latQuery, CultureInfo.InvariantCulture, out var lat) || !double.TryParse(lngQuery, CultureInfo.InvariantCulture, out var lng))
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid coordinates", detail: "lat and lng must be valid numbers.");
    }

    if (lat is < 1.15 or > 1.48 || lng is < 103.58 or > 104.09)
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Coordinates out of bounds", detail: "lat and lng must be within Singapore bounds.");
    }

    var radius = 500;
    if (!string.IsNullOrWhiteSpace(radiusQuery))
    {
        if (!int.TryParse(radiusQuery, out radius) || radius is <= 0 or > 500)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid radius", detail: "radius must be a positive integer no greater than 500 metres.");
        }
    }

    return Results.Ok(carparkService.GetNearby(lat, lng, radius));
});

app.MapGet("/api/carparks/{carparkNo}", (string carparkNo, CarparkService carparkService) =>
{
    if (string.IsNullOrWhiteSpace(carparkNo))
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Missing car park number", detail: "carparkNo is required.");
    }

    var result = carparkService.GetByNo(carparkNo);
    return result is null
        ? Results.NotFound(new ProblemDetails { Title = "Car park not found", Detail = $"Car park '{carparkNo}' was not found in the HDB dataset.", Status = StatusCodes.Status404NotFound })
        : Results.Ok(result);
});

app.MapDefaultEndpoints();

app.Run();
