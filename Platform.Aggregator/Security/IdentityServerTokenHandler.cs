using System.Net.Http.Headers;

namespace Platform.Aggregator.Security;

public sealed class IdentityServerTokenHandler(
    IdentityServerTokenProvider tokenProvider)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetAccessTokenAsync(
            cancellationToken);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
