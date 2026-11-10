using MediatR;
using Nimbus.Contracts.DTOs.Features.Weather;
namespace Nimbus.Application.Features.Weather.Queries.GetHourlyForecast;

/// <summary>The hourly forecast for a place. <paramref name="Model" /> defaults to the configured model (icon_seamless).</summary>
public sealed record GetHourlyForecastQuery(double Latitude, double Longitude, string? Model = null) : IRequest<HourlyForecastDto>;
