namespace Nimbus.Application.Common.Interfaces;

/// <summary>
///     Abstraction over the application's custom business metrics
///     Implemented in the API layer with prometheus-net so the Application layer stays
///     free of infrastructure concerns.
/// </summary>
public interface IBusinessMetrics
{
    /// <summary>
    ///     Increment the counter for the number of times users are fetched by email
    /// </summary>
    void UserFetchedByEmail();
}
