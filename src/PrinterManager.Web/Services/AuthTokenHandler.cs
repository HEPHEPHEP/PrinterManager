using System.Net.Http.Headers;

namespace PrinterManager.Web.Services;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly AuthStateService _authState;

    public AuthTokenHandler(AuthStateService authState)
    {
        _authState = authState;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_authState.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authState.Token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
