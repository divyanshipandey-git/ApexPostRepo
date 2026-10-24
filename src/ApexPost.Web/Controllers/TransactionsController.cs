using System.Security.Claims;
using ApexPost.Application.Services.Transactions;
using ApexPost.Application.Services.Staff;
using ApexPost.Domain.Entities;
using ApexPost.Web.Models.Transactions;
using ApexPost.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize]
public sealed class TransactionsController(
    TransactionVoidService transactionVoidService,
    CounterSaleService counterSaleService) : Controller
{
    [Authorize(Policy = AuthorizationPolicies.CounterSales)]
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var staff = CurrentStaff();
        if (staff is null)
        {
            return Forbid();
        }

        var options = await counterSaleService.GetOptionsAsync(staff, cancellationToken);
        if (options is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var model = CreateViewModel(options);
        return View(model);
    }

    [Authorize(Policy = AuthorizationPolicies.CounterSales)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CounterSaleViewModel model,
        CancellationToken cancellationToken)
    {
        var staff = CurrentStaff();
        if (staff is null)
        {
            return Forbid();
        }

        var submittedProducts = model.Products ?? [];
        var productLines = new List<CounterSaleProductRequest>();
        for (var index = 0; index < submittedProducts.Count; index++)
        {
            var line = submittedProducts[index];
            var hasSku = !string.IsNullOrWhiteSpace(line.Sku);
            var hasQuantity = line.Quantity.HasValue;
            if (!hasSku && !hasQuantity)
            {
                continue;
            }

            if (!hasSku || !hasQuantity || line.Quantity is <= 0 or > 1000)
            {
                ModelState.AddModelError(
                    $"Products[{index}].Sku",
                    "Choose a product and enter a quantity from 1 to 1000.");
                continue;
            }

            productLines.Add(new CounterSaleProductRequest(line.Sku!, line.Quantity.GetValueOrDefault()));
        }

        if (submittedProducts.Count > 50)
        {
            ModelState.AddModelError(string.Empty, "A transaction can contain no more than 50 product lines.");
        }

        if (productLines.Count == 0 && !model.IncludeShipping && !model.IncludeMoneyOrder)
        {
            ModelState.AddModelError(string.Empty, "Add at least one product, shipping service, or money order.");
        }

        CounterMoneyOrderRequest? moneyOrder = null;
        var hasVerificationInput = model.IdentityVerified ||
            !string.IsNullOrWhiteSpace(model.IdentityDocumentType) ||
            !string.IsNullOrWhiteSpace(model.IdentityMaskedDocumentNumber);
        if (model.IncludeMoneyOrder)
        {
            if (model.MoneyOrderAmount is null or <= 0 or > 1_000_000_000m ||
                string.IsNullOrWhiteSpace(model.MoneyOrderSenderName) ||
                model.MoneyOrderSenderName.Length > 120 ||
                string.IsNullOrWhiteSpace(model.MoneyOrderRecipientName) ||
                model.MoneyOrderRecipientName.Length > 120)
            {
                ModelState.AddModelError(nameof(model.MoneyOrderAmount), "Enter a positive amount and sender and recipient names.");
            }
            else
            {
                CounterIdentityVerificationRequest? verification = null;
                if (hasVerificationInput)
                {
                    if (string.IsNullOrWhiteSpace(model.IdentityDocumentType) ||
                        model.IdentityDocumentType.Length > 80 ||
                        string.IsNullOrWhiteSpace(model.IdentityMaskedDocumentNumber) ||
                        model.IdentityMaskedDocumentNumber.Length > 32)
                    {
                        ModelState.AddModelError(nameof(model.IdentityMaskedDocumentNumber), "Enter the document type and a masked number ending in four characters.");
                    }
                    else
                    {
                        verification = new CounterIdentityVerificationRequest(
                            model.IdentityDocumentType,
                            model.IdentityMaskedDocumentNumber,
                            model.IdentityVerified);
                    }
                }

                moneyOrder = new CounterMoneyOrderRequest(
                    model.MoneyOrderAmount.Value,
                    model.MoneyOrderSenderName,
                    model.MoneyOrderRecipientName,
                    verification);
            }
        }
        else if (model.MoneyOrderAmount.HasValue ||
                 !string.IsNullOrWhiteSpace(model.MoneyOrderSenderName) ||
                 !string.IsNullOrWhiteSpace(model.MoneyOrderRecipientName) ||
                 hasVerificationInput)
        {
            ModelState.AddModelError(nameof(model.IncludeMoneyOrder), "Select money order to include its details.");
        }

        CounterShippingRequest? shipping = null;
        if (model.IncludeShipping)
        {
            if (!Enum.TryParse<ShippingServiceLevel>(model.ShippingServiceLevel, out var level) ||
                !Enum.IsDefined(level) ||
                string.IsNullOrWhiteSpace(model.ItemType) ||
                model.WeightKg is null or <= 0 ||
                model.LengthCm is null or <= 0 ||
                model.WidthCm is null or <= 0 ||
                model.HeightCm is null or <= 0 ||
                string.IsNullOrWhiteSpace(model.DestinationZone))
            {
                ModelState.AddModelError(nameof(model.IncludeShipping), "Complete the shipping details with valid positive dimensions and a supported service level.");
            }
            else
            {
                shipping = new CounterShippingRequest(
                    model.ItemType,
                    model.WeightKg.Value,
                    model.LengthCm.Value,
                    model.WidthCm.Value,
                    model.HeightCm.Value,
                    model.DestinationZone,
                    level);
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, staff, cancellationToken);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(model);
        }

        if (!Enum.TryParse<PaymentMethod>(model.PaymentMethod, out var paymentMethod) ||
            !Enum.IsDefined(paymentMethod))
        {
            ModelState.AddModelError(nameof(model.PaymentMethod), "Select a valid payment method.");
            await PopulateOptionsAsync(model, staff, cancellationToken);
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(model);
        }

        var result = await counterSaleService.CompleteAsync(
            new CounterSaleRequest(model.CounterId!, productLines, paymentMethod, shipping, moneyOrder),
            staff,
            cancellationToken);

        if (result.Status == CounterSaleStatus.Completed)
        {
            return RedirectToAction(nameof(Receipt), new { id = result.Transaction!.Id });
        }

        await PopulateOptionsAsync(model, staff, cancellationToken);
        return result.Status switch
        {
            CounterSaleStatus.InvalidProduct => SaleValidationError(model, "Select a product that exists in the catalogue."),
            CounterSaleStatus.InsufficientStock => SaleConflict(model, "There is not enough stock to complete this sale."),
            CounterSaleStatus.BranchClosed => SaleConflict(model, "This branch is closed and cannot accept transactions."),
            CounterSaleStatus.ShippingUnavailable => SaleValidationError(model, "The selected shipping service is unavailable or its details are invalid."),
            CounterSaleStatus.MoneyOrderVerificationRequired => SaleValidationError(model, "This money order amount requires successful identity verification. No order was issued."),
            CounterSaleStatus.CounterNotFound or CounterSaleStatus.CounterInactive => SaleValidationError(model, "Select an active counter assigned to this branch."),
            CounterSaleStatus.StaffInactive or CounterSaleStatus.StaffNotFound or CounterSaleStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
            CounterSaleStatus.BranchNotFound => NotFound(),
            CounterSaleStatus.PersistenceFailure => SalePersistenceError(model),
            _ => SaleValidationError(model, "Review the transaction details and try again.")
        };
    }

    [Authorize(Policy = AuthorizationPolicies.CounterSales)]
    [HttpGet]
    public async Task<IActionResult> Receipt(string? id, CancellationToken cancellationToken)
    {
        var staff = CurrentStaff();
        if (staff is null)
        {
            return Forbid();
        }

        var result = await counterSaleService.GetReceiptAsync(id ?? string.Empty, staff, cancellationToken);
        if (result.Status == CounterSaleStatus.TransactionNotFound)
        {
            return NotFound();
        }

        if (result.Status == CounterSaleStatus.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var transaction = result.Transaction!;
        var shipmentId = transaction.LineItems.FirstOrDefault(line => line.ReferenceId is not null)?.ReferenceId;

        return View(new TransactionReceiptViewModel(
            transaction.Id,
            transaction.BranchCode.Value,
            transaction.CounterId,
            transaction.Status.ToString(),
            transaction.PaymentMethod?.ToString() ?? string.Empty,
            transaction.LineItems.Select(line => new TransactionReceiptLineViewModel(
                line.Description,
                line.ItemType,
                line.Quantity,
                line.UnitPrice.Amount,
                line.LineTotal.Amount,
                line.TaxCategory.ToString())).ToArray(),
            transaction.Subtotal.Amount,
            transaction.Tax.Amount,
            transaction.Total.Amount,
            shipmentId));
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var transactions = await transactionVoidService.GetBranchTransactionsAsync(
            User.FindFirstValue("StaffId"),
            cancellationToken);
        if (transactions is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return View(transactions.Select(transaction => new TransactionSummaryViewModel(
            transaction.Id,
            transaction.BranchCode.Value,
            transaction.Status.ToString(),
            transaction.Total.Amount)).ToArray());
    }

    [HttpGet]
    public async Task<IActionResult> Details(string? id, CancellationToken cancellationToken)
    {
        var result = await transactionVoidService.GetAccessibleAsync(
            id ?? string.Empty,
            User.FindFirstValue("StaffId"),
            cancellationToken);

        return result.Status switch
        {
            TransactionVoidStatus.Available => View(ToViewModel(result.Transaction!)),
            TransactionVoidStatus.NotFound => NotFound(),
            TransactionVoidStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
            TransactionVoidStatus.InvalidTransactionId => BadRequest(),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(
        TransactionVoidViewModel model,
        CancellationToken cancellationToken)
    {
        var staffId = User.FindFirstValue("StaffId");
        if (!ModelState.IsValid)
        {
            var accessible = await transactionVoidService.GetAccessibleAsync(
                model.TransactionId,
                staffId,
                cancellationToken);
            if (accessible.Status == TransactionVoidStatus.Forbidden)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            if (accessible.Status == TransactionVoidStatus.NotFound)
            {
                return NotFound();
            }

            if (accessible.Status != TransactionVoidStatus.Available)
            {
                return BadRequest();
            }

            var invalidViewModel = ToViewModel(accessible.Transaction!);
            invalidViewModel.VoidReason = model.VoidReason;
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View("Details", invalidViewModel);
        }

        var result = await transactionVoidService.VoidAsync(
            model.TransactionId,
            model.VoidReason,
            staffId,
            cancellationToken);

        return result.Status switch
        {
            TransactionVoidStatus.Voided => RedirectToAction(
                nameof(Details),
                new { id = result.Transaction!.Id }),
            TransactionVoidStatus.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
            TransactionVoidStatus.NotFound => NotFound(),
            TransactionVoidStatus.InvalidReason => InvalidReasonView(
                WithVoidReason(result.Transaction!, model.VoidReason)),
            TransactionVoidStatus.InvalidState => InvalidStateView(
                WithVoidReason(result.Transaction!, model.VoidReason)),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    private IActionResult InvalidReasonView(TransactionVoidViewModel model)
    {
        ModelState.AddModelError(nameof(model.VoidReason), "Enter a reason for voiding this transaction.");
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View("Details", model);
    }

    private IActionResult InvalidStateView(TransactionVoidViewModel model)
    {
        model.Message = "Only paid transactions can be voided.";
        Response.StatusCode = StatusCodes.Status409Conflict;
        return View("Details", model);
    }

    private StaffRole? CurrentRole =>
        Enum.TryParse<StaffRole>(User.FindFirstValue("Role"), ignoreCase: false, out var role) &&
        Enum.IsDefined(role)
            ? role
            : null;

    private AuthenticatedStaff? CurrentStaff()
    {
        var staffId = User.FindFirstValue("StaffId");
        var branchCode = User.FindFirstValue("BranchCode");
        var role = CurrentRole;
        return string.IsNullOrWhiteSpace(staffId) ||
               string.IsNullOrWhiteSpace(branchCode) ||
               role is null
            ? null
            : new AuthenticatedStaff(staffId, role.Value, branchCode);
    }

    private async Task PopulateOptionsAsync(
        CounterSaleViewModel model,
        AuthenticatedStaff staff,
        CancellationToken cancellationToken)
    {
        var options = await counterSaleService.GetOptionsAsync(staff, cancellationToken);
        model.CounterOptions = options?.Counters
            .Select(counter => new CounterSaleOptionViewModel(counter.Id, counter.Name))
            .ToArray() ?? [];
        model.ProductOptions = options?.Products
            .Select(product => new CounterSaleProductOptionViewModel(
                product.Sku.Value,
                product.Name,
                product.UnitPrice.Amount,
                product.TaxCategory.ToString()))
            .ToArray() ?? [];
        var knownSkus = model.ProductOptions.Select(option => option.Sku)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        model.ProductOptions = model.ProductOptions.Concat(
                (model.Products ?? [])
                    .Where(line => !string.IsNullOrWhiteSpace(line.Sku) && knownSkus.Add(line.Sku))
                    .Select(line => new CounterSaleProductOptionViewModel(
                        line.Sku!,
                        $"Unavailable product ({line.Sku})",
                        0m,
                        string.Empty)))
            .ToArray();
        model.MoneyOrderFee = options?.MoneyOrderFee ?? 0m;
        model.MoneyOrderVerificationThreshold = options?.MoneyOrderVerificationThreshold ?? 0m;
    }

    private static CounterSaleViewModel CreateViewModel(CounterSaleOptions? options) => new()
    {
        Products = Enumerable.Range(0, 5)
            .Select(_ => new CounterSaleProductLineViewModel())
            .ToList(),
        CounterOptions = options?.Counters
            .Select(counter => new CounterSaleOptionViewModel(counter.Id, counter.Name))
            .ToArray() ?? [],
        ProductOptions = options?.Products
            .Select(product => new CounterSaleProductOptionViewModel(
                product.Sku.Value,
                product.Name,
                product.UnitPrice.Amount,
                product.TaxCategory.ToString()))
            .ToArray() ?? [],
        MoneyOrderFee = options?.MoneyOrderFee ?? 0m,
        MoneyOrderVerificationThreshold = options?.MoneyOrderVerificationThreshold ?? 0m
    };

    private IActionResult SaleValidationError(CounterSaleViewModel model, string message)
    {
        ModelState.AddModelError(string.Empty, message);
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View("Create", model);
    }

    private IActionResult SaleConflict(CounterSaleViewModel model, string message)
    {
        ModelState.AddModelError(string.Empty, message);
        Response.StatusCode = StatusCodes.Status409Conflict;
        return View("Create", model);
    }

    private IActionResult SalePersistenceError(CounterSaleViewModel model)
    {
        model.Message = "The transaction could not be saved. No sale was completed; please retry or contact a supervisor.";
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View("Create", model);
    }

    private static TransactionVoidViewModel ToViewModel(CounterTransaction transaction) => new()
    {
        TransactionId = transaction.Id,
        BranchCode = transaction.BranchCode.Value,
        Status = transaction.Status.ToString(),
        Total = transaction.Total.Amount,
        ExistingReason = transaction.Reason
    };

    private static TransactionVoidViewModel WithVoidReason(
        CounterTransaction transaction,
        string? reason)
    {
        var viewModel = ToViewModel(transaction);
        viewModel.VoidReason = reason;
        return viewModel;
    }
}
