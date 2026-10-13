using MediatR;
using Nimbus.Contracts.DTOs.Features.OfpSettings;
namespace Nimbus.Application.Features.OfpSettings.Queries.GetMyOfpSettings;

public sealed record GetMyOfpSettingsQuery : IRequest<OfpSettingsDto>;
