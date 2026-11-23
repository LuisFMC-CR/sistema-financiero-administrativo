using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.Customers;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Application.Catalogs.WithholdingTypes;
using SistemaFinanciero.Application.Receivables;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Web.Controllers;
using SistemaFinanciero.Web.Models.Receivables;

namespace SistemaFinanciero.IntegrationTests.Receivables;

/// <summary>
/// Regla 3 y RF-95 en el cobro distribuido: la retención y su base solo se ofrecen a clientes del
/// sector público; la base no se edita en el reparto y el servidor la recalcula con el monto
/// aplicado, sin confiar en la que llegue en el formulario. Factura de ₡100.000,00 con neto
/// ₡88.495,58: el pago completo de un cliente del sector público es ₡98.230,09 con base exacta.
/// </summary>
public sealed class ReceivablePaymentAllocationWithholdingTests
{
    private const decimal InvoiceNet = 88_495.58m;
    private const decimal InvoiceTotal = 100_000.00m;
    private const decimal FullPaymentAfterWithholding = 98_230.09m;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ReceivableId = Guid.NewGuid();
    private static readonly Guid CashAccountId = Guid.NewGuid();
    private static readonly Guid WithholdingTypeId = Guid.NewGuid();

    [Fact]
    public async Task Start_ForACustomerWithoutStateWithholding_OffersNoWithholdingAndProposesNoBase()
    {
        ReceivablePaymentsController controller = CreateController(appliesStateWithholding: false, out _);

        IActionResult result = await controller.Start(StartModel(InvoiceTotal), CancellationToken.None);

        RegisterReceivablePaymentAllocateViewModel model = Assert.IsType<RegisterReceivablePaymentAllocateViewModel>(
            Assert.IsType<ViewResult>(result).Model);
        Assert.Empty(model.WithholdingTypeOptions);
        ReceivablePaymentAllocationLineViewModel line = Assert.Single(model.Allocations);
        Assert.Equal(InvoiceTotal, line.Amount);
        Assert.Equal(0m, line.WithholdingBase);
    }

    [Fact]
    public async Task Start_ForAStateWithholdingCustomer_OffersTheWithholdingAndProposesTheExactBase()
    {
        ReceivablePaymentsController controller = CreateController(appliesStateWithholding: true, out _);

        IActionResult result = await controller.Start(StartModel(FullPaymentAfterWithholding), CancellationToken.None);

        RegisterReceivablePaymentAllocateViewModel model = Assert.IsType<RegisterReceivablePaymentAllocateViewModel>(
            Assert.IsType<ViewResult>(result).Model);
        Assert.Single(model.WithholdingTypeOptions);
        ReceivablePaymentAllocationLineViewModel line = Assert.Single(model.Allocations);
        Assert.Equal(InvoiceNet, line.WithholdingBase);
        Assert.Equal(InvoiceTotal, line.OriginalAmount);
    }

    [Fact]
    public async Task Register_RecalculatesTheBaseFromTheAppliedAmountAndIgnoresThePostedOne()
    {
        ReceivablePaymentsController controller = CreateController(appliesStateWithholding: true, out Func<RegisterReceivablePaymentCommand?> lastCommand);

        await controller.Register(
            AllocateModel(FullPaymentAfterWithholding, WithholdingTypeId, postedBase: 1m),
            CancellationToken.None);

        ReceivablePaymentAllocationCommand allocation = Assert.Single(lastCommand()!.Allocations);
        Assert.Equal(FullPaymentAfterWithholding, allocation.Amount);
        Assert.NotNull(allocation.Withholding);
        Assert.Equal(WithholdingTypeId, allocation.Withholding.WithholdingTypeId);
        Assert.Equal(InvoiceNet, allocation.Withholding.Base);
    }

    [Fact]
    public async Task Register_WithoutAWithholdingType_SendsNoWithholdingEvenIfABaseIsPosted()
    {
        ReceivablePaymentsController controller = CreateController(appliesStateWithholding: false, out Func<RegisterReceivablePaymentCommand?> lastCommand);

        await controller.Register(
            AllocateModel(InvoiceTotal, withholdingTypeId: null, postedBase: 5_000m),
            CancellationToken.None);

        ReceivablePaymentAllocationCommand allocation = Assert.Single(lastCommand()!.Allocations);
        Assert.Null(allocation.Withholding);
    }

    [Fact]
    public async Task Start_WithAFutureDate_IsRejectedAndDoesNotProposeAnAllocation()
    {
        ReceivablePaymentsController controller = CreateController(appliesStateWithholding: false, out _);
        RegisterReceivablePaymentStartViewModel model = StartModel(InvoiceTotal);
        model.PaymentDate = new DateOnly(2026, 11, 21);

        IActionResult result = await controller.Start(model, CancellationToken.None);

        Assert.IsType<RegisterReceivablePaymentStartViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(model.PaymentDate)));
    }

    [Fact]
    public async Task Start_WithTodaysDate_IsAccepted()
    {
        ReceivablePaymentsController controller = CreateController(appliesStateWithholding: false, out _);
        RegisterReceivablePaymentStartViewModel model = StartModel(InvoiceTotal);
        model.PaymentDate = new DateOnly(2026, 11, 20);

        IActionResult result = await controller.Start(model, CancellationToken.None);

        Assert.IsType<RegisterReceivablePaymentAllocateViewModel>(Assert.IsType<ViewResult>(result).Model);
    }

    [Fact]
    public async Task Register_WithAFutureDate_DoesNotRegisterTheReceipt()
    {
        ReceivablePaymentsController controller = CreateController(appliesStateWithholding: false, out Func<RegisterReceivablePaymentCommand?> lastCommand);
        RegisterReceivablePaymentAllocateViewModel model = AllocateModel(InvoiceTotal, withholdingTypeId: null, postedBase: 0m);
        model.PaymentDate = new DateOnly(2026, 11, 21);

        IActionResult result = await controller.Register(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Null(lastCommand());
    }

    private static RegisterReceivablePaymentStartViewModel StartModel(decimal amount) => new()
    {
        CustomerId = CustomerId,
        CashAccountId = CashAccountId,
        Reference = "TRF-001",
        PaymentDate = new DateOnly(2026, 11, 18),
        Amount = amount,
    };

    private static RegisterReceivablePaymentAllocateViewModel AllocateModel(
        decimal amount,
        Guid? withholdingTypeId,
        decimal postedBase) => new()
    {
        CustomerId = CustomerId,
        CashAccountId = CashAccountId,
        Reference = "TRF-001",
        PaymentDate = new DateOnly(2026, 11, 18),
        Allocations =
        [
            new ReceivablePaymentAllocationLineViewModel
            {
                ReceivableId = ReceivableId,
                Amount = amount,
                WithholdingTypeId = withholdingTypeId,
                WithholdingBase = postedBase,
            },
        ],
    };

    private static ReceivablePaymentsController CreateController(
        bool appliesStateWithholding,
        out Func<RegisterReceivablePaymentCommand?> lastCommand)
    {
        DateTimeOffset now = new(2026, 11, 18, 15, 0, 0, TimeSpan.Zero);
        ReceivableModel receivable = new(
            ReceivableId, Guid.NewGuid(), 1001, null, InvoiceNet, CustomerId, "Universidad Pública",
            InvoiceTotal, InvoiceTotal, new DateOnly(2026, 12, 18), ReceivableStatus.Pending, now, "v1");
        CustomerModel customer = new(
            CustomerId, "CLI-0001", "Universidad Pública", IdentificationType.LegalEntity, "4000042149",
            null, null, null, null, VatCondition.Normal, appliesStateWithholding, null, true, now, now, "v1");
        RegisterReceivablePaymentCommand? captured = null;

        IReceivableService receivables = InterfaceStub.Create<IReceivableService>(new()
        {
            [nameof(IReceivableService.GetPendingByCustomerAsync)] = _ =>
                Task.FromResult<IReadOnlyList<ReceivableModel>>([receivable]),
        });
        ICustomerCatalogService customers = InterfaceStub.Create<ICustomerCatalogService>(new()
        {
            [nameof(ICustomerCatalogService.GetAsync)] = _ => Task.FromResult<CustomerModel?>(customer),
            [nameof(ICustomerCatalogService.GetActiveOptionsAsync)] = _ =>
                Task.FromResult<IReadOnlyList<CustomerOption>>([]),
        });
        ILedgerAccountService ledger = InterfaceStub.Create<ILedgerAccountService>(new()
        {
            [nameof(ILedgerAccountService.GetActiveCashAccountsAsync)] = _ =>
                Task.FromResult<IReadOnlyList<CashAccountOption>>(
                    [new CashAccountOption(CashAccountId, "1.1.02", "Cuenta bancaria principal", CashAccountKind.Bank)]),
        });
        IWithholdingTypeService withholdingTypes = InterfaceStub.Create<IWithholdingTypeService>(new()
        {
            [nameof(IWithholdingTypeService.GetActiveOptionsAsync)] = _ =>
                Task.FromResult<IReadOnlyList<WithholdingTypeOption>>(
                    [new WithholdingTypeOption(WithholdingTypeId, "RET-2", "Retención 2 % sector público", 2m, null, true)]),
        });
        IReceivablePaymentService payments = InterfaceStub.Create<IReceivablePaymentService>(new()
        {
            [nameof(IReceivablePaymentService.RegisterAsync)] = args =>
            {
                captured = (RegisterReceivablePaymentCommand)args![0]!;
                return Task.FromResult(new RegisterReceivablePaymentResult(
                    CatalogOperationResult.Succeeded(), [Guid.NewGuid()], false));
            },
        });
        lastCommand = () => captured;

        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
                authenticationType: "Test")),
        };

        return new ReceivablePaymentsController(
            receivables, payments, customers, ledger, withholdingTypes,
            FixedLocalTimeProvider.AtCostaRicaLocalTime(new DateTime(2026, 11, 20, 10, 0, 0)))
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new NullTempDataProvider()),
        };
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}

/// <summary>
/// Doble de prueba para interfaces grandes: responde solo los métodos indicados por nombre y falla
/// con claridad si el código llama a cualquier otro.
/// </summary>
public class InterfaceStub : DispatchProxy
{
    private Dictionary<string, Func<object?[]?, object?>> handlers = [];

    public static T Create<T>(Dictionary<string, Func<object?[]?, object?>> handlers)
        where T : class
    {
        T proxy = Create<T, InterfaceStub>();
        ((InterfaceStub)(object)proxy).handlers = handlers;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        return handlers.TryGetValue(targetMethod.Name, out Func<object?[]?, object?>? handler)
            ? handler(args)
            : throw new NotSupportedException($"La prueba no esperaba la llamada a {targetMethod.Name}.");
    }
}
