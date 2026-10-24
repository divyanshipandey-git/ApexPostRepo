namespace ApexPost.Application.Services.Transactions;

public enum CounterSaleStatus
{
    Completed,
    InvalidRequest,
    TransactionNotFound,
    InvalidProduct,
    StaffNotFound,
    StaffInactive,
    Forbidden,
    BranchNotFound,
    BranchClosed,
    CounterNotFound,
    CounterInactive,
    InsufficientStock,
    ShippingUnavailable,
    PersistenceFailure
}
