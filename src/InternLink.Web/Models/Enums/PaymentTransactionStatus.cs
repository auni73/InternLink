namespace InternLink.Web.Models.Enums;

public enum PaymentTransactionStatus : byte
{
    Initiated = 0,
    Completed = 1,
    Failed = 2,
    Cancelled = 3,
    Refunded = 4
}
