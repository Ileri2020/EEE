using EEE.Services;
using Microsoft.Maui.ApplicationModel;

namespace EEE;

public partial class AccountPage : ContentPage
{
    private readonly RemoteEeeApiClient _remoteApi;

    public AccountPage(RemoteEeeApiClient remoteApi)
    {
        InitializeComponent();
        _remoteApi = remoteApi;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAccountStatusAsync();
    }

    private async void OnOpenAccountClicked(object? sender, EventArgs e)
    {
        try
        {
            await Browser.Default.OpenAsync(new Uri($"{RemoteEeeApiClient.ApiBaseUrl}/account"), BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            AccountStatusLabel.Text = $"Could not open HCVP account: {ex.Message}";
        }
    }

    private async void OnConnectClicked(object? sender, EventArgs e)
    {
        try
        {
            AccountStatusLabel.Text = "Verifying token…";
            await _remoteApi.ConnectAccountAsync(AppTokenEntry.Text ?? string.Empty);
            AppTokenEntry.Text = string.Empty;
            await RefreshAccountStatusAsync();
        }
        catch (Exception ex)
        {
            AccountStatusLabel.Text = $"Connection failed: {ex.Message}";
        }
    }

    private async void OnDisconnectClicked(object? sender, EventArgs e)
    {
        try
        {
            await _remoteApi.DisconnectAccountAsync();
            await RefreshAccountStatusAsync();
        }
        catch (Exception ex)
        {
            AccountStatusLabel.Text = $"Could not revoke the app token: {ex.Message}";
        }
    }

    private async Task RefreshAccountStatusAsync()
    {
        try
        {
            var status = await _remoteApi.GetAccountStatusAsync();
            AccountStatusLabel.Text = status.SignedIn
                ? $"Connected{(string.IsNullOrWhiteSpace(status.User?.Email) ? string.Empty : $" as {status.User.Email}")}{(status.ApiKeyConfigured ? " · personal Gemini key active" : " · using server Gemini key")}" 
                : "Not connected. AI continues to use the HCVP server key.";
            DisconnectButton.IsVisible = status.SignedIn;
        }
        catch (Exception ex)
        {
            AccountStatusLabel.Text = $"Account status unavailable: {ex.Message}";
            DisconnectButton.IsVisible = false;
        }
    }
}
