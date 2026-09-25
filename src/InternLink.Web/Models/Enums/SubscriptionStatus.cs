namespace InternLink.Web.Models.Enums;

public enum SubscriptionStatus : byte
{
    PendingPayment = 0,
    Active = 1,
    Expired = 2,
    Suspended = 3,
    Cancelled = 4
}
