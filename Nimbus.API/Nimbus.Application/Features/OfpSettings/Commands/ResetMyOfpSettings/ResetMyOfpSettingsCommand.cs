using MediatR;
using Nimbus.Contracts.DTOs.Features.OfpSettings;
namespace Nimbus.Application.Features.OfpSettings.Commands.ResetMyOfpSettings;

public sealed record ResetMyOfpSettingsCommand : IRequest<OfpSettingsDto>;
