using MobileAppDeployment.Application.Validation;
using MobileAppDeployment.Core.Domain.Entities;

namespace MobileAppDeployment.Tests;

/// <summary>
/// Unit tests for <see cref="AppDeploymentValidation.NormalizeNonNullableStringsForPartialSave"/>.
/// </summary>
public class AppDeploymentValidationTests
{
    /// <summary>
    /// Coerces null non-nullable string properties to safe defaults for partial saves.
    /// </summary>
    [Fact]
    public void NormalizeNonNullableStringsForPartialSave_ReplacesNullsWithDefaults()
    {
        var model = new AppDeployment
        {
            CommonName = null!,
            OrganizationUnit = null!,
            OrganizationName = null!,
            AppName = null!,
            Country = null!,
            OneSignalRestApiKey = null!
        };

        AppDeploymentValidation.NormalizeNonNullableStringsForPartialSave(model);

        Assert.Equal(string.Empty, model.CommonName);
        Assert.Equal(string.Empty, model.OrganizationUnit);
        Assert.Equal(string.Empty, model.OrganizationName);
        Assert.Equal(string.Empty, model.AppName);
        Assert.Equal("US", model.Country);
        Assert.Equal(string.Empty, model.OneSignalRestApiKey);
    }
}
