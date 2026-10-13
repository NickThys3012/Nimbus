using MediatR;
using Nimbus.Application.Common.Interfaces;
using Nimbus.Contracts.DTOs.Features.GoNoGoThresholds;
using Nimbus.Contracts.Mappers;
using Nimbus.Domain.Interfaces;
namespace Nimbus.Application.Features.GoNoGoThresholds.Queries.GetMyGoNoGoThresholds;

public sealed class GetMyGoNoGoThresholdsQueryHandler : IRequestHandler<GetMyGoNoGoThresholdsQuery, GoNoGoThresholdsDto>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IGoNoGoThresholdsRepository _repository;

    public GetMyGoNoGoThresholdsQueryHandler(IGoNoGoThresholdsRepository repository, ICurrentUserService currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<GoNoGoThresholdsDto> Handle(GetMyGoNoGoThresholdsQuery request, CancellationToken ct)
    {
        return await GoNoGoThresholdsResolver.ResolveAsync(_repository, _currentUser.UserId, ct);
    }
}

/// <summary>User row, else global default row (read-only fallback), else the built-in defaults.</summary>
internal static class GoNoGoThresholdsResolver
{
    public static async Task<GoNoGoThresholdsDto> ResolveAsync(IGoNoGoThresholdsRepository repository, Guid userId, CancellationToken ct)
    {
        var mine = await repository.GetByOwnerAsync(userId, ct);
        if (mine is not null)
        {
            return mine.MapToDto(true);
        }

        var global = await repository.GetGlobalDefaultAsync(ct);
        return (global ?? GoNoGoThresholdDefaults.BuiltIn()).MapToDto(false);
    }
}
