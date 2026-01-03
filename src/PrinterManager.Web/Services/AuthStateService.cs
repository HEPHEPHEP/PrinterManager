namespace PrinterManager.Web.Services;

public class AuthStateService
{
    private string? _token;
    private string? _username;
    private string? _role;

    public event Action? OnChange;

    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);
    public string? Username => _username;
    public string? Role => _role;
    public string? Token => _token;

    public void SetAuthInfo(string token, string username, string role)
    {
        _token = token;
        _username = username;
        _role = role;
        NotifyStateChanged();
    }

    public void ClearAuthInfo()
    {
        _token = null;
        _username = null;
        _role = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
