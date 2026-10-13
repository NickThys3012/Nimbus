using MediatR;
using Nimbus.Contracts.DTOs.Features.GoNoGoThresholds;
namespace Nimbus.Application.Features.GoNoGoThresholds.Queries.GetMyGoNoGoThresholds;

public sealed record GetMyGoNoGoThresholdsQuery : IRequest<GoNoGoThresholdsDto>;
