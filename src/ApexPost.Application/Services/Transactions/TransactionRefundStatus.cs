namespace ApexPost.Application.Services.Transactions;

public enum TransactionRefundStatus
{
    Refunded,
    NotFound,
    Forbidden,
    InvalidReason,
    InvalidState,
    InvalidTransactionId
}
