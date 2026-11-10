using MediatR;
using Nimbus.Contracts.DTOs.Features.Solar;
namespace Nimbus.Application.Features.Solar.Queries.GetSunTimes;

/// <summary>Sunrise and sunset at a place on a local calendar <paramref name="Date" />. <paramref name="TimeZone" /> (an IANA id such as Europe/Brussels) is optional: with it the local times are returned too.</summary>
public sealed record GetSunTimesQuery(double Latitude, double Longitude, DateOnly Date, string? TimeZone = null) : IRequest<SunTimesDto>;
