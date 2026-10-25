namespace ApexPost.Application.Services.Inventory;

public enum InventoryInquiryStatus
{
    Found,
    InvalidBranchCode,
    InvalidSku,
    BranchNotFound,
    ProductNotFound,
    StockNotFound
}
