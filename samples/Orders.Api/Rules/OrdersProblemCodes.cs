namespace Orders.Api.Rules;

/// <summary>
/// The codes this application's rules report. A prefix of its own keeps them clear of RF, which
/// RequestFlow reserves, and of any package contributing rules of its own.
/// </summary>
public static class OrdersProblemCodes
{
    public const string RequestSuffix = "ORDERS0001";

    public const string CommandNotValidated = "ORDERS0002";
}
