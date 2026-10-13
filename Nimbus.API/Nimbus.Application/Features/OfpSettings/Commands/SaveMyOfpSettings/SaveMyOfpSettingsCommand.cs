using MediatR;
using Nimbus.Contracts.DTOs.Features.OfpSettings;
namespace Nimbus.Application.Features.OfpSettings.Commands.SaveMyOfpSettings;

public sealed record SaveMyOfpSettingsCommand(OfpSettingsRequestDto Settings) : IRequest<OfpSettingsDto>;
