using FluentAssertions;
using IntelliMed.Core.Entities;
using IntelliMed.Infrastructure.Services;
using Xunit;

namespace IntelliMed.Tests.Services;

public class PrimaryClinicLegacyMapperTests
{
    [Theory]
    [InlineData("M", GenderEnum.Male)]
    [InlineData("m", GenderEnum.Male)]
    [InlineData("F", GenderEnum.Female)]
    [InlineData("f", GenderEnum.Female)]
    [InlineData(null, GenderEnum.Unspecified)]
    [InlineData("", GenderEnum.Unspecified)]
    [InlineData("X", GenderEnum.Unspecified)]
    public void MapGender_TranslatesLegacyCodes(string? code, GenderEnum expected)
    {
        PrimaryClinicLegacyMapper.MapGender(code).Should().Be(expected);
    }

    [Theory]
    [InlineData("Y", DobAccuracyEnum.Year)]
    [InlineData("M", DobAccuracyEnum.Month)]
    [InlineData("E", DobAccuracyEnum.Estimated)]
    [InlineData(null, DobAccuracyEnum.Day)]
    [InlineData("", DobAccuracyEnum.Day)]
    [InlineData("AAA", DobAccuracyEnum.Day)]
    public void MapDobAccuracy_UnrecognizedCodesDefaultToDay(string? code, DobAccuracyEnum expected)
    {
        PrimaryClinicLegacyMapper.MapDobAccuracy(code).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true, AtsiStatusEnum.Both)]
    [InlineData(true, false, AtsiStatusEnum.AboriginalOnly)]
    [InlineData(false, true, AtsiStatusEnum.TorresStraitIslanderOnly)]
    [InlineData(false, false, AtsiStatusEnum.NotAsked)]
    public void MapAtsiStatus_CombinesBothFlags(bool isAboriginal, bool isTsi, AtsiStatusEnum expected)
    {
        PrimaryClinicLegacyMapper.MapAtsiStatus(isAboriginal, isTsi).Should().Be(expected);
    }

    [Theory]
    [InlineData(1, MaritalStatusEnum.Single)]
    [InlineData(2, MaritalStatusEnum.Married)]
    [InlineData(3, MaritalStatusEnum.Divorced)]
    [InlineData(4, MaritalStatusEnum.Widowed)]
    [InlineData(5, MaritalStatusEnum.DeFacto)]
    [InlineData(6, MaritalStatusEnum.Separated)]
    public void MapMaritalStatus_KnownCodesTranslate(int code, MaritalStatusEnum expected)
    {
        PrimaryClinicLegacyMapper.MapMaritalStatus(code).Should().Be(expected);
    }

    [Fact]
    public void MapMaritalStatus_NullOrUnknownCodeReturnsNull()
    {
        PrimaryClinicLegacyMapper.MapMaritalStatus(null).Should().BeNull();
        PrimaryClinicLegacyMapper.MapMaritalStatus(99).Should().BeNull();
    }

    [Theory]
    [InlineData("Private Patient", AccountTypeEnum.PrivatePatient)]
    [InlineData("DVA", AccountTypeEnum.Veteran)]
    [InlineData("WorkCover", AccountTypeEnum.WorkCover)]
    [InlineData("TAC", AccountTypeEnum.Tac)]
    [InlineData("IMC", AccountTypeEnum.Imc)]
    [InlineData("BB", AccountTypeEnum.BulkBill)]
    [InlineData("Health Fund", AccountTypeEnum.PrivatePatient)]
    [InlineData("OVS", AccountTypeEnum.Other)]
    [InlineData("Imm'n", AccountTypeEnum.Other)]
    [InlineData("Others", AccountTypeEnum.Other)]
    [InlineData("No Charge", AccountTypeEnum.Other)]
    [InlineData("Credit Note", AccountTypeEnum.Other)]
    public void MapAccountType_KnownLegacyNamesTranslate(string legacyName, AccountTypeEnum expected)
    {
        PrimaryClinicLegacyMapper.MapAccountType(legacyName).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Some Unrecognized Legacy Account Type")]
    public void MapAccountType_UnrecognizedOrMissingNameFallsBackToOther(string? legacyName)
    {
        PrimaryClinicLegacyMapper.MapAccountType(legacyName).Should().Be(AccountTypeEnum.Other);
    }

    [Fact]
    public void MapAccountType_IsCaseInsensitive()
    {
        PrimaryClinicLegacyMapper.MapAccountType("workcover").Should().Be(AccountTypeEnum.WorkCover);
        PrimaryClinicLegacyMapper.MapAccountType("dva").Should().Be(AccountTypeEnum.Veteran);
    }

    [Theory]
    [InlineData("Cash", PaymentMethod.Cash)]
    [InlineData("Cheque", PaymentMethod.Cheque)]
    [InlineData("Debit Card", PaymentMethod.Eftpos)]
    [InlineData("Visa", PaymentMethod.CreditCard)]
    [InlineData("Master", PaymentMethod.CreditCard)]
    [InlineData("AMEX", PaymentMethod.CreditCard)]
    [InlineData("Diners", PaymentMethod.CreditCard)]
    [InlineData("Direct Credit", PaymentMethod.BankTransfer)]
    [InlineData("Others", PaymentMethod.Other)]
    [InlineData("Credit allocation", PaymentMethod.Other)]
    [InlineData("Discount", PaymentMethod.Other)]
    [InlineData("Write off", PaymentMethod.Other)]
    public void MapPaymentMethod_KnownLegacyNamesTranslate(string legacyName, PaymentMethod expected)
    {
        PrimaryClinicLegacyMapper.MapPaymentMethod(legacyName).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Some Unrecognized Legacy Payment Type")]
    public void MapPaymentMethod_UnrecognizedOrMissingNameFallsBackToOther(string? legacyName)
    {
        PrimaryClinicLegacyMapper.MapPaymentMethod(legacyName).Should().Be(PaymentMethod.Other);
    }

    [Theory]
    [InlineData(true, PlaceOfServiceEnum.Hospital)]
    [InlineData(false, PlaceOfServiceEnum.Rooms)]
    public void MapPlaceOfService_TranslatesHospitalIndFlag(bool anyItemHospitalInd, PlaceOfServiceEnum expected)
    {
        PrimaryClinicLegacyMapper.MapPlaceOfService(anyItemHospitalInd).Should().Be(expected);
    }
}
