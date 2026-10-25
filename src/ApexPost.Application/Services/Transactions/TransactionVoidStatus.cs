namespace ApexPost.Application.Services.Transactions;

public enum TransactionVoidStatus
{
    Available,
    Voided,
    NotFound,
    Forbidden,
    InvalidReason,
    InvalidState,
    InvalidTransactionId
}
