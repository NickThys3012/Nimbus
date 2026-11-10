using MediatR;
using Nimbus.Contracts.DTOs.Features.Weather;
namespace Nimbus.Application.Features.Weather.Queries.GetMetarTaf;

public sealed record GetMetarTafQuery(string IcaoCode) : IRequest<MetarTafDto>;
