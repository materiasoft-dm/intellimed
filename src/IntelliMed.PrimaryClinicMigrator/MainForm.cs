using System.ComponentModel;

namespace IntelliMed.PrimaryClinicMigrator;

public partial class MainForm : Form
{
    private readonly Label _lblServer = new() { Text = "SQL Server:", AutoSize = true };
    private readonly TextBox _txtServer = new() { Text = ".", Width = 200 };
    private readonly Label _lblUser = new() { Text = "User:", AutoSize = true };
    private readonly TextBox _txtUser = new() { Text = "sa", Width = 200 };
    private readonly Label _lblPassword = new() { Text = "Password:", AutoSize = true };
    private readonly TextBox _txtPassword = new() { Text = "", Width = 200, UseSystemPasswordChar = true };
    private readonly Label _lblDatabase = new() { Text = "Database:", AutoSize = true };
    private readonly TextBox _txtDatabase = new() { Text = "PracnetSample_Rosie", Width = 200 };
    private readonly Label _lblApiUrl = new() { Text = "API URL:", AutoSize = true };
    private readonly TextBox _txtApiUrl = new() { Text = "http://localhost:5284", Width = 300 };
    private readonly Label _lblAdminEmail = new() { Text = "Admin Email:", AutoSize = true };
    private readonly TextBox _txtAdminEmail = new() { Text = "admin@clinic.com", Width = 300 };
    private readonly Label _lblAdminPassword = new() { Text = "Admin Password:", AutoSize = true };
    private readonly TextBox _txtAdminPassword = new() { Text = "", Width = 300, UseSystemPasswordChar = true };
    private readonly Label _lblClinicId = new() { Text = "Clinic ID:", AutoSize = true };
    private readonly NumericUpDown _numClinicId = new() { Minimum = 1, Maximum = int.MaxValue, Value = 1, Width = 80 };
    private readonly Button _btnStart = new() { Text = "Start Migration", Width = 140, Height = 36 };
    private readonly Button _btnTestConnection = new() { Text = "Test Connection", Width = 120, Height = 36 };
    private readonly ProgressBar _progress = new() { Style = ProgressBarStyle.Marquee, Visible = false, Height = 20 };
    private readonly RichTextBox _log = new() { ReadOnly = true, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.LightGray, Font = new Font("Consolas", 9) };
    private readonly BackgroundWorker _worker = new() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };

    public MainForm()
    {
        Text = "IntelliMed — Primary Clinic Migrator";
        Size = new Size(800, 740);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9);

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 13
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Row 0: Server
        panel.Controls.Add(_lblServer, 0, 0);
        panel.Controls.Add(_txtServer, 1, 0);
        // Row 1: User
        panel.Controls.Add(_lblUser, 0, 1);
        panel.Controls.Add(_txtUser, 1, 1);
        // Row 2: Password
        panel.Controls.Add(_lblPassword, 0, 2);
        panel.Controls.Add(_txtPassword, 1, 2);
        // Row 3: Database
        panel.Controls.Add(_lblDatabase, 0, 3);
        panel.Controls.Add(_txtDatabase, 1, 3);
        // Row 4: API URL
        panel.Controls.Add(_lblApiUrl, 0, 4);
        panel.Controls.Add(_txtApiUrl, 1, 4);
        // Row 5: Admin Email
        panel.Controls.Add(_lblAdminEmail, 0, 5);
        panel.Controls.Add(_txtAdminEmail, 1, 5);
        // Row 6: Admin Password
        panel.Controls.Add(_lblAdminPassword, 0, 6);
        panel.Controls.Add(_txtAdminPassword, 1, 6);
        // Row 7: Clinic ID
        panel.Controls.Add(_lblClinicId, 0, 7);
        panel.Controls.Add(_numClinicId, 1, 7);
        // Row 8: Buttons
        var buttonPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        buttonPanel.Controls.Add(_btnTestConnection);
        buttonPanel.Controls.Add(_btnStart);
        panel.Controls.Add(buttonPanel, 1, 8);
        // Row 9: Progress
        panel.Controls.Add(_progress, 1, 9);
        // Row 10-12: Log (spans 2 cols)
        panel.SetColumnSpan(_log, 2);
        panel.Controls.Add(_log, 0, 10);
        panel.SetRowSpan(_log, 3);

        Controls.Add(panel);

        _btnTestConnection.Click += async (_, _) => await TestConnectionAsync();
        _btnStart.Click += (_, _) => StartMigration();
        _worker.DoWork += Worker_DoWork;
        _worker.ProgressChanged += (_, e) =>
        {
            if (e.UserState is LogMessage msg)
                AppendLog(msg.Text, msg.Color);
            else
                AppendLog(e.UserState?.ToString() ?? "");
        };
        _worker.RunWorkerCompleted += (_, e) =>
        {
            _progress.Visible = false;
            _btnStart.Enabled = true;
            _btnTestConnection.Enabled = true;
            if (e.Error != null)
                AppendLog($"ERROR: {e.Error.Message}", Color.Red);
            else if (e.Cancelled)
                AppendLog("Migration cancelled.", Color.Orange);
            else
                AppendLog("Migration completed successfully.", Color.Lime);
        };
    }

    private async Task TestConnectionAsync()
    {
        try
        {
            AppendLog("Testing SQL Server connection...");
            var extractor = new LegacyDataExtractor(_txtServer.Text, _txtUser.Text, _txtPassword.Text, _txtDatabase.Text);
            var clients = await extractor.ExtractClientsAsync();
            AppendLog($"OK — found {clients.Count:N0} patients in the legacy database.", Color.Lime);
        }
        catch (Exception ex)
        {
            AppendLog($"Connection failed: {ex.Message}", Color.Red);
        }
    }

    private void StartMigration()
    {
        _btnStart.Enabled = false;
        _btnTestConnection.Enabled = false;
        _progress.Visible = true;
        _log.Clear();
        _worker.RunWorkerAsync();
    }

    private async void Worker_DoWork(object? sender, DoWorkEventArgs e)
    {
        var extractor = new LegacyDataExtractor(_txtServer.Text, _txtUser.Text, _txtPassword.Text, _txtDatabase.Text);
        var api = new ApiClient(_txtApiUrl.Text);
        var clinicId = (int)_numClinicId.Value;

        Report("Logging in...");
        var (loginOk, loginError) = await api.LoginAsync(_txtAdminEmail.Text, _txtAdminPassword.Text);
        if (!loginOk)
        {
            Report($"Login failed: {loginError}", Color.Red);
            return;
        }
        Report("Logged in.", Color.Lime);

        // 1. Clients
        Report("Extracting clients...");
        var clients = await extractor.ExtractClientsAsync();
        Report($"Extracted {clients.Count:N0} clients. Sending to API...");
        var result = await api.PostImportAsync("api/primary-clinic-migrator/import-clients", clients, clinicId);
        Report($"Clients: {result.ClientsCreated} created, {result.ClientsUpdated} updated.");

        // 2. Addresses
        Report("Extracting addresses...");
        var addresses = await extractor.ExtractAddressesAsync();
        Report($"Extracted {addresses.Count:N0} addresses. Sending to API...");
        result = await api.PostImportAsync("api/primary-clinic-migrator/import-addresses", addresses, clinicId);
        Report($"Addresses: {result.AddressesCreated} created, {result.AddressesUpdated} updated.");

        // 3. Referrals
        Report("Extracting referrals...");
        var referrals = await extractor.ExtractReferralsAsync();
        Report($"Extracted {referrals.Count:N0} referrals. Sending to API...");
        result = await api.PostImportAsync("api/primary-clinic-migrator/import-referrals", referrals, clinicId);
        Report($"Referrals: {result.ReferralsCreated} created, {result.ReferralsUpdated} updated.");

        // 4. Occupations
        Report("Extracting occupations...");
        var occupations = await extractor.ExtractOccupationsAsync();
        Report($"Extracted {occupations.Count:N0} occupations. Sending to API...");
        result = await api.PostImportAsync("api/primary-clinic-migrator/import-occupations", occupations, clinicId);
        Report($"Occupations: {result.OccupationsCreated} created, {result.OccupationsUpdated} updated.");

        // 5. Family Relationships
        Report("Extracting family relationships...");
        var family = await extractor.ExtractFamilyRelationshipsAsync();
        Report($"Extracted {family.Count:N0} family relationships. Sending to API...");
        result = await api.PostImportAsync("api/primary-clinic-migrator/import-family-relationships", family, clinicId);
        Report($"Family: {result.FamilyRelationshipsCreated} created, {result.FamilyRelationshipsUpdated} updated.");

        // 6. Compensation Claims
        Report("Extracting compensation claims...");
        var claims = await extractor.ExtractCompensationClaimsAsync();
        Report($"Extracted {claims.Count:N0} compensation claims. Sending to API...");
        result = await api.PostImportAsync("api/primary-clinic-migrator/import-compensation-claims", claims, clinicId);
        Report($"Claims: {result.CompensationClaimsCreated} created, {result.CompensationClaimsUpdated} updated.");

        // 7. Invoices
        Report("Extracting invoices...");
        var invoices = await extractor.ExtractInvoicesAsync();
        Report($"Extracted {invoices.Count:N0} invoices. Sending to API...");
        result = await api.PostImportAsync("api/primary-clinic-migrator/import-invoices", invoices, clinicId);
        Report($"Invoices: {result.InvoicesCreated} created, {result.InvoicesUpdated} updated.");

        // 8. Invoice Items
        Report("Extracting invoice items...");
        var invoiceItems = await extractor.ExtractInvoiceItemsAsync();
        Report($"Extracted {invoiceItems.Count:N0} invoice items. Sending to API...");
        result = await api.PostImportAsync("api/primary-clinic-migrator/import-invoice-items", invoiceItems, clinicId);
        Report($"Invoice Items: {result.InvoiceItemsCreated} created, {result.InvoiceItemsUpdated} updated.");

        // 9. Payments (allocations + payments)
        Report("Extracting payment allocations...");
        var allocations = await extractor.ExtractPaymentAllocationsAsync();
        Report($"Extracted {allocations.Count:N0} payment allocations.");
        Report("Extracting payments...");
        var payments = await extractor.ExtractPaymentsAsync();
        Report($"Extracted {payments.Count:N0} payments. Sending to API...");
        result = await api.PostPaymentsImportAsync(allocations, payments, clinicId);
        Report($"Payments: {result.PaymentsCreated} created, {result.PaymentsUpdated} updated.");

        if (result.Warnings.Count > 0)
        {
            Report($"Warnings ({result.Warnings.Count}):");
            foreach (var w in result.Warnings)
                Report($"  - {w}");
        }

        Report("Migration complete!", Color.Lime);
    }

    private void Report(string message, Color? color = null)
    {
        _worker.ReportProgress(0, new LogMessage { Text = message, Color = color });
    }

    private void AppendLog(string message, Color? color = null)
    {
        _log.SelectionStart = _log.TextLength;
        _log.SelectionLength = 0;
        _log.SelectionColor = color ?? Color.LightGray;
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        _log.ScrollToCaret();
    }

    private record LogMessage { public string Text = ""; public Color? Color; }
}
