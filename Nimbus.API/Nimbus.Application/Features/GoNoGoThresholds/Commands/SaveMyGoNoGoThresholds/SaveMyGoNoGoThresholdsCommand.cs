using MediatR;
using Nimbus.Contracts.DTOs.Features.GoNoGoThresholds;
namespace Nimbus.Application.Features.GoNoGoThresholds.Commands.SaveMyGoNoGoThresholds;

public sealed record SaveMyGoNoGoThresholdsCommand(GoNoGoThresholdsRequestDto Thresholds) : IRequest<GoNoGoThresholdsDto>;
