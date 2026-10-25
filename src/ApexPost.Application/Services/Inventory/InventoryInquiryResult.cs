using ApexPost.Domain.Entities;

namespace ApexPost.Application.Services.Inventory;

public sealed record InventoryInquiryResult(
    InventoryInquiryStatus Status,
    Branch? Branch = null,
    Product? Product = null,
    InventoryStock? Stock = null);
