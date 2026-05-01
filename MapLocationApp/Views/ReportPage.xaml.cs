using System.Globalization;
using System.Text;
using MapLocationApp.Models;
using MapLocationApp.Services;

namespace MapLocationApp.Views;

public partial class ReportPage : ContentPage
{
    private readonly IReportService _reportService;
    private readonly IUserSessionService _session;
    private WorkType? _filter;

    public ReportPage(IReportService reportService, IUserSessionService session)
    {
        InitializeComponent();
        _reportService = reportService;
        _session = session;
        SetThisMonth();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        LocalizationService.Instance.CultureChanged -= OnCultureChanged;
        LocalizationService.Instance.CultureChanged += OnCultureChanged;
        await UpdateSummaryAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        LocalizationService.Instance.CultureChanged -= OnCultureChanged;
    }

    private void OnCultureChanged(CultureInfo _) => MainThread.BeginInvokeOnMainThread(async () => await UpdateSummaryAsync());

    private void SetThisMonth()
    {
        var first = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        StartDatePicker.Date = first;
        EndDatePicker.Date = first.AddMonths(1).AddDays(-1);
    }

    private void OnThisMonthClicked(object sender, EventArgs e)
    {
        SetThisMonth();
        _ = UpdateSummaryAsync();
    }

    private void OnLastMonthClicked(object sender, EventArgs e)
    {
        var firstThis = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        StartDatePicker.Date = firstThis.AddMonths(-1);
        EndDatePicker.Date = firstThis.AddDays(-1);
        _ = UpdateSummaryAsync();
    }

    private void OnLast30DaysClicked(object sender, EventArgs e)
    {
        EndDatePicker.Date = DateTime.Today;
        StartDatePicker.Date = DateTime.Today.AddDays(-29);
        _ = UpdateSummaryAsync();
    }

    private async void OnDateChanged(object? sender, DateChangedEventArgs e) => await UpdateSummaryAsync();

    private async void OnFilterChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (sender is RadioButton rb && rb.IsChecked)
        {
            _filter = ReferenceEquals(rb, FilterOfficeRadio) ? WorkType.Office
                    : ReferenceEquals(rb, FilterFieldRadio) ? WorkType.Field
                    : (WorkType?)null;
            await UpdateSummaryAsync();
        }
    }

    private async Task<List<CheckInRecord>> FetchAsync()
    {
        var user = await _session.GetCurrentUserAsync();
        if (user == null) return new List<CheckInRecord>();
        return await _reportService.GetRecordsAsync(user.Id,
            StartDatePicker.Date, EndDatePicker.Date.AddDays(1).AddSeconds(-1), _filter);
    }

    private async Task UpdateSummaryAsync()
    {
        var records = await FetchAsync();
        SummaryLabel.Text = string.Format(L("TotalRecords"), records.Count);
    }

    private async void OnExportExcelClicked(object sender, EventArgs e)
    {
        try
        {
            var records = await FetchAsync();
            var path = BuildExportPath("xlsx");
            var ok = await _reportService.ExportToExcelAsync(records, path, BuildTitle());
            await DisplayAlert(ok ? L("Success") : L("Error"),
                ok ? string.Format(L("ExportSuccess"), path) : L("ExportFailed"), L("OK"));
        }
        catch (Exception ex) { await DisplayAlert(L("Error"), ex.Message, L("OK")); }
    }

    private async void OnExportPdfClicked(object sender, EventArgs e)
    {
        try
        {
            var records = await FetchAsync();
            var path = BuildExportPath("pdf");
            var ok = await _reportService.ExportToPdfAsync(records, path, BuildTitle());
            await DisplayAlert(ok ? L("Success") : L("Error"),
                ok ? string.Format(L("ExportSuccess"), path) : L("ExportFailed"), L("OK"));
        }
        catch (Exception ex) { await DisplayAlert(L("Error"), ex.Message, L("OK")); }
    }

    private async void OnExportCsvClicked(object sender, EventArgs e)
    {
        try
        {
            var records = await FetchAsync();
            var path = BuildExportPath("csv");
            var sb = new StringBuilder();
            sb.AppendLine("Date,CheckIn,CheckOut,Location,WorkType,Method,Notes,EditedAt");
            foreach (var r in records)
            {
                sb.Append(r.CheckInTime.ToString("yyyy-MM-dd")).Append(',');
                sb.Append(r.CheckInTime.ToString("HH:mm")).Append(',');
                sb.Append(r.CheckOutTime?.ToString("HH:mm") ?? "").Append(',');
                sb.Append(Csv(r.GeofenceName)).Append(',');
                sb.Append(r.WorkType).Append(',');
                sb.Append(r.Method).Append(',');
                sb.Append(Csv(r.Notes ?? "")).Append(',');
                sb.AppendLine(r.EditedAt?.ToString("yyyy-MM-dd HH:mm") ?? "");
            }
            await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8);
            await DisplayAlert(L("Success"), string.Format(L("ExportSuccess"), path), L("OK"));
        }
        catch (Exception ex) { await DisplayAlert(L("Error"), ex.Message, L("OK")); }
    }

    private static string Csv(string s) => s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

    private string BuildExportPath(string ext)
    {
        var dir = Path.Combine(FileSystem.AppDataDirectory, "exports");
        Directory.CreateDirectory(dir);
        var fname = $"checkins_{StartDatePicker.Date:yyyyMMdd}_{EndDatePicker.Date:yyyyMMdd}_{DateTime.Now:HHmmss}.{ext}";
        return Path.Combine(dir, fname);
    }

    private string BuildTitle()
    {
        var f = _filter switch { WorkType.Office => L("Office"), WorkType.Field => L("Field"), _ => L("WorkTypeAll") };
        return $"{L("MyReport")} {StartDatePicker.Date:yyyy-MM-dd} ~ {EndDatePicker.Date:yyyy-MM-dd} ({f})";
    }

    private static string L(string key) => LocalizationService.Instance.GetLocalizedString(key);
}
