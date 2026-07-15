using MobileAppDeployment.Core.Domain.Entities;

namespace MobileAppDeployment.Core.Models.ViewModels;

/// <summary>
/// Lightweight list-row view model for the admin deployment index page.
/// </summary>
public class AppDeploymentListItemViewModel
{
    public int Id { get; set; }
    public string AppName { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }

    /// <summary>
    /// Maps a domain entity to a list item view model.
    /// </summary>
    /// <param name="entity">Deployment entity from the repository.</param>
    /// <returns>A list item safe for the Index view.</returns>
    public static AppDeploymentListItemViewModel FromEntity(AppDeployment entity)
    {
        return new AppDeploymentListItemViewModel
        {
            Id = entity.Id,
            AppName = entity.AppName,
            OrganizationName = entity.OrganizationName,
            CreatedDate = entity.CreatedDate,
            ModifiedDate = entity.ModifiedDate
        };
    }
}
