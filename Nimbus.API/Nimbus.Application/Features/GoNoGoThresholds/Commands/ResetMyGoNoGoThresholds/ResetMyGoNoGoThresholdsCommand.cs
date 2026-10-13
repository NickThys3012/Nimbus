using MediatR;
using Nimbus.Contracts.DTOs.Features.GoNoGoThresholds;
namespace Nimbus.Application.Features.GoNoGoThresholds.Commands.ResetMyGoNoGoThresholds;

public sealed record ResetMyGoNoGoThresholdsCommand : IRequest<GoNoGoThresholdsDto>;
