using MediatR;
using Nimbus.Contracts.DTOs.Features.Weather;
namespace Nimbus.Application.Features.Weather.Queries.GetWindSeries;

/// <summary>Hourly wind snapshots for a place from <paramref name="FromUtc" /> to <paramref name="ToUtc" />. <paramref name="Model" /> defaults to the configured model.</summary>
public sealed record GetWindSeriesQuery(double Latitude, double Longitude, DateTime FromUtc, DateTime ToUtc, string? Model = null) : IRequest<WindSeriesDto>;
