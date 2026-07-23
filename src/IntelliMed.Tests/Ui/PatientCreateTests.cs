using Microsoft.Playwright;
using Xunit;

namespace IntelliMed.Tests.Ui;

[Collection("Playwright UI")]
public class PatientCreateTests
{
    private readonly PlaywrightServerFixture _fixture;

    public PatientCreateTests(PlaywrightServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddPatientPage_Loads_WithAllExpectedPanels()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/patients/add");
        await page.WaitForSelectorAsync("text=New Patient");

        await Assertions.Expect(page.Locator(".card-header:has-text('Personal')")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".card-header:has-text('Entitlement')")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".card-header:has-text('Contact Details')")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".card-header:has-text('Health Fund')")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".card-header:has-text('Account')")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(".card-header:has-text('File')")).ToBeVisibleAsync();

        foreach (var tab in new[] { "Referrals", "WC/TAC", "Family", "Occupations", "eHealth", "Lifecard", "UDF" })
        {
            await Assertions.Expect(page.TabLink(tab)).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task CreatePatient_WithMinimalFields_NavigatesToEditPage()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/patients/add");
        await page.WaitForSelectorAsync("text=New Patient");

        await page.Field("Surname").FillAsync("Minimal");
        await page.Field("Given").FillAsync("Patient");

        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();

        await page.WaitForURLAsync(url => url.Contains("/patients/edit/"));
        await Assertions.Expect(page.Locator("h5")).ToContainTextAsync("Edit Patient — Patient Minimal");
    }

    [Theory]
    [InlineData("Unspecified")]
    [InlineData("Male")]
    [InlineData("Female")]
    [InlineData("Other")]
    public async Task CreatePatient_WithEachGender_PersistsCorrectly(string genderValue)
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/patients/add");
        await page.WaitForSelectorAsync("text=New Patient");

        await page.Field("Surname").FillAsync($"Gender{genderValue}");
        await page.Field("Given").FillAsync("Test");
        await page.Field("Gender").SelectOptionAsync(genderValue);

        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/patients/edit/"));

        await Assertions.Expect(page.Field("Gender")).ToHaveValueAsync(genderValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Single")]
    [InlineData("Married")]
    [InlineData("DeFacto")]
    [InlineData("Divorced")]
    [InlineData("Widowed")]
    [InlineData("Separated")]
    [InlineData("Unknown")]
    public async Task CreatePatient_WithEachMaritalStatus_PersistsCorrectly(string maritalValue)
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/patients/add");
        await page.WaitForSelectorAsync("text=New Patient");

        await page.Field("Surname").FillAsync($"Marital{maritalValue}");
        await page.Field("Given").FillAsync("Test");
        await page.Field("Marital Status").SelectOptionAsync(maritalValue);

        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/patients/edit/"));

        await Assertions.Expect(page.Field("Marital Status")).ToHaveValueAsync(maritalValue);
    }

    [Theory]
    [InlineData("NotAsked")]
    [InlineData("AboriginalOnly")]
    [InlineData("TorresStraitIslanderOnly")]
    [InlineData("Both")]
    [InlineData("NeitherAboriginalNorTorresStraitIslander")]
    public async Task CreatePatient_WithEachAtsiStatus_PersistsCorrectly(string atsiValue)
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/patients/add");
        await page.WaitForSelectorAsync("text=New Patient");

        await page.Field("Surname").FillAsync($"Atsi{atsiValue}");
        await page.Field("Given").FillAsync("Test");
        await page.Field("ATSI").SelectOptionAsync(atsiValue);

        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/patients/edit/"));

        await Assertions.Expect(page.Field("ATSI")).ToHaveValueAsync(atsiValue);
    }

    [Theory]
    [InlineData("PrivatePatient")]
    [InlineData("Concession")]
    [InlineData("Pensioner")]
    [InlineData("Veteran")]
    [InlineData("WorkCover")]
    [InlineData("Tac")]
    [InlineData("BulkBill")]
    [InlineData("Other")]
    public async Task CreatePatient_WithEachAccountType_PersistsCorrectly(string accountTypeValue)
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/patients/add");
        await page.WaitForSelectorAsync("text=New Patient");

        await page.Field("Surname").FillAsync($"Account{accountTypeValue}");
        await page.Field("Given").FillAsync("Test");
        await page.Field("Acc. Type").SelectOptionAsync(accountTypeValue);

        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/patients/edit/"));

        await Assertions.Expect(page.Field("Acc. Type")).ToHaveValueAsync(accountTypeValue);
    }

    [Theory]
    [InlineData("Day")]
    [InlineData("Month")]
    [InlineData("Year")]
    [InlineData("Estimated")]
    public async Task CreatePatient_WithEachDobAccuracy_PersistsCorrectly(string accuracyValue)
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/patients/add");
        await page.WaitForSelectorAsync("text=New Patient");

        await page.Field("Surname").FillAsync($"Dob{accuracyValue}");
        await page.Field("Given").FillAsync("Test");
        await page.Field("Accuracy").SelectOptionAsync(accuracyValue);

        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/patients/edit/"));

        await Assertions.Expect(page.Field("Accuracy")).ToHaveValueAsync(accuracyValue);
    }

    [Fact]
    public async Task CreatePatient_WithFullDetailsAcrossAllPanels_PersistsEverything()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/patients/add");
        await page.WaitForSelectorAsync("text=New Patient");

        // Personal
        await page.Field("Surname").FillAsync("Doe");
        await page.Field("Given").FillAsync("John");
        await page.Field("Middle").FillAsync("Michael");
        await page.Field("Preferred").FillAsync("Johnny");
        await page.Field("Gender").SelectOptionAsync("Male");
        await page.Field("D.O.B").FillAsync("1985-06-15");
        await page.Field("Ethnicity").FillAsync("Caucasian");

        // Entitlement
        await page.FieldInCard("Entitlement", "Medicare#").FillAsync("1234567890");

        // Residential address (default active tab)
        await page.Field("Address").FillAsync("123 Main Street");
        await page.Field("Suburb").FillAsync("Melbourne");
        await page.Field("Postcode").FillAsync("3000");
        await page.Field("State").FillAsync("VIC");

        // Contact Details
        await page.Field("Home Phone").FillAsync("0398765432");
        await page.Field("Email").FillAsync("john.doe@example.com");

        // Health Fund
        await page.FieldInCard("Health Fund", "Fund Id").SelectOptionAsync(new SelectOptionValue { Label = "BUP - Bupa" });

        // File
        await page.Field("File#").FillAsync("FILE001");

        await page.GetByRole(AriaRole.Button, new() { Name = "💾 Save" }).ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/patients/edit/"));

        await Assertions.Expect(page.Field("Surname")).ToHaveValueAsync("Doe");
        await Assertions.Expect(page.Field("Given")).ToHaveValueAsync("John");
        await Assertions.Expect(page.Field("Middle")).ToHaveValueAsync("Michael");
        await Assertions.Expect(page.Field("Preferred")).ToHaveValueAsync("Johnny");
        await Assertions.Expect(page.Field("Gender")).ToHaveValueAsync("Male");
        await Assertions.Expect(page.Field("Ethnicity")).ToHaveValueAsync("Caucasian");
        await Assertions.Expect(page.FieldInCard("Entitlement", "Medicare#")).ToHaveValueAsync("1234567890");
        await Assertions.Expect(page.Field("Address")).ToHaveValueAsync("123 Main Street");
        await Assertions.Expect(page.Field("Suburb")).ToHaveValueAsync("Melbourne");
        await Assertions.Expect(page.Field("Postcode")).ToHaveValueAsync("3000");
        await Assertions.Expect(page.Field("State")).ToHaveValueAsync("VIC");
        await Assertions.Expect(page.Field("Home Phone")).ToHaveValueAsync("0398765432");
        await Assertions.Expect(page.Field("Email")).ToHaveValueAsync("john.doe@example.com");
        await Assertions.Expect(page.FieldInCard("Health Fund", "Fund Id")).ToHaveValueAsync("2");
        await Assertions.Expect(page.Field("File#")).ToHaveValueAsync("FILE001");
    }

    [Fact]
    public async Task InterpreterCheckbox_WhenChecked_EnablesLanguageField()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/patients/add");
        await page.WaitForSelectorAsync("text=New Patient");

        await Assertions.Expect(page.Field("Language")).ToBeDisabledAsync();

        await page.GetByLabel("Interpreter?").CheckAsync();

        await Assertions.Expect(page.Field("Language")).ToBeEnabledAsync();
        await page.Field("Language").FillAsync("Italian");
        await Assertions.Expect(page.Field("Language")).ToHaveValueAsync("Italian");
    }

    [Fact]
    public async Task SameAsNextOfKinCheckbox_WhenChecked_DisablesEmergencyFields()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/patients/add");
        await page.WaitForSelectorAsync("text=New Patient");

        await Assertions.Expect(page.Field("Emergency")).ToBeEnabledAsync();

        await page.GetByLabel("Same as Next of Kin").CheckAsync();

        await Assertions.Expect(page.Field("Emergency")).ToBeDisabledAsync();
        await Assertions.Expect(page.Field("Emergency Phone")).ToBeDisabledAsync();
    }

    [Fact]
    public async Task AddressTabs_SwitchBetweenResidentialPostalAndOther()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/patients/add");
        await page.WaitForSelectorAsync("text=New Patient");

        // Residential is active by default and uses the flat Patient.Address field.
        await Assertions.Expect(page.Field("Address")).ToBeVisibleAsync();

        // Postal/Other are new patient-scoped addresses; before the patient is saved there's no
        // PatientId to attach them to, so the "Save Address" button should be disabled.
        await page.TabLink("Postal").ClickAsync();
        await Assertions.Expect(page.GetByText("Save the patient first to add this address.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Save Address" })).ToBeDisabledAsync();

        await page.TabLink("Other Addresses").ClickAsync();
        await Assertions.Expect(page.GetByText("Save the patient first to add this address.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task BottomTabs_BeforeSaving_ShowSavePatientFirstMessage()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/patients/add");
        await page.WaitForSelectorAsync("text=New Patient");

        foreach (var tab in new[] { "Referrals", "WC/TAC", "Family", "Occupations", "eHealth", "Lifecard", "UDF" })
        {
            await page.TabLink(tab).ClickAsync();
            await Assertions.Expect(page.GetByText($"Save the patient first to manage {tab}.")).ToBeVisibleAsync();
        }

        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "New Family" })).ToBeDisabledAsync();
    }

    [Fact]
    public async Task CloseButton_NavigatesBackToPatientList()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync("/patients/add");
        await page.WaitForSelectorAsync("text=New Patient");

        await page.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();

        await page.WaitForURLAsync(url => url.EndsWith("/patients"));
    }
}
