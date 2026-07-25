using IntelliMed.Core.Entities;

namespace IntelliMed.Infrastructure.Services;

/// <summary>
/// Money/fee rounding helpers matching legacy Pracnet behaviour: banker's rounding
/// (MidpointRounding.ToEven) for money totals, and the fee schedule's RoundType for fees.
/// </summary>
public static class BillingMath
{
    /// <summary>Round a monetary amount to 2 dp using banker's rounding, as legacy BillingCommon.RoundMoney does.</summary>
    public static decimal RoundMoney(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.ToEven);

    /// <summary>
    /// Apply a fee schedule's rounding method to a fee. All variants normalise to at most 2 dp;
    /// ToNearest5c rounds to the nearest 5 cents. Uses banker's rounding to match legacy.
    /// </summary>
    public static decimal ApplyScheduleRounding(decimal fee, RoundingTypeEnum roundingType) => roundingType switch
    {
        RoundingTypeEnum.ToNearest5c => Math.Round(fee * 20m, 0, MidpointRounding.ToEven) / 20m,
        _ => RoundMoney(fee) // Exact / ToNearest1c — round to the nearest cent
    };
}
