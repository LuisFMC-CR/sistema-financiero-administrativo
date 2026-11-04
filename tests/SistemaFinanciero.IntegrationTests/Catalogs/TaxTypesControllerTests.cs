using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Web.Controllers;

namespace SistemaFinanciero.IntegrationTests.Catalogs;

public sealed class TaxTypesControllerTests
{
    [Fact]
    public void Index_RedirectsToTheUnifiedTaxesAndWithholdingsScreen()
    {
        TaxTypesController controller = new();

        IActionResult result = controller.Index();

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("TaxesAndWithholdings", redirect.ControllerName);
    }
}
