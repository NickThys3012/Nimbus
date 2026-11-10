using MediatR;
using Nimbus.Contracts.DTOs.Features.Weather;
namespace Nimbus.Application.Features.Weather.Queries.GetWindProfile;

/// <summary>Wind by altitude at a place for one hour. <paramref name="HourUtc" /> defaults to the current hour, <paramref name="Model" /> to icon_seamless.</summary>
public sealed record GetWindProfileQuery(double Latitude, double Longitude, DateTime? HourUtc = null, string? Model = null) : IRequest<WindProfileDto>;
