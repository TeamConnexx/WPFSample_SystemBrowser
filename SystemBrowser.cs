using System;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IdentityModel.OidcClient.Browser;

internal class SystemBrowser : IBrowser
{
    const string ERROR_MESSAGE = "Error ocurred.";
    const string SUCCESSFUL_AUTHENTICATION_MESSAGE = "You have been successfully authenticated. You can now continue to use desktop application.";
    const string SUCCESSFUL_LOGOUT_MESSAGE = "You have been successfully logged out.";
    private HttpListener _httpListener;
    private void StartSystemBrowser(string startUrl)
    {
        Process.Start(new ProcessStartInfo(startUrl) { UseShellExecute = true });
    }
    public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
    {
        StartSystemBrowser(options.StartUrl);
        var result = new BrowserResult();

        //abort _httpListener if exists
        _httpListener?.Abort();
        using (_httpListener = new HttpListener())
        {
            var listenUrl = options.EndUrl;

            //HttpListenerContext require uri ends with /
            if (!listenUrl.EndsWith("/"))
                listenUrl += "/";

            _httpListener.Prefixes.Add(listenUrl);
            _httpListener.Start();
            using (cancellationToken.Register(() =>
            {
                _httpListener?.Abort();
            }))
            {
                HttpListenerContext context;
                try
                {
                    context = await _httpListener.GetContextAsync();
                }
                //if _httpListener is aborted while waiting for response it throws HttpListenerException exception
                catch (HttpListenerException)
                {
                    result.ResultType = BrowserResultType.UnknownError;
                    return result;
                }

                //set result response url
                result.Response = context.Request.Url.AbsoluteUri;

                //generate message displayed in the browser, and set resultType based on request
                string displayMessage;
                if (context.Request.QueryString.Get("code") != null)
                {
                    displayMessage = SUCCESSFUL_AUTHENTICATION_MESSAGE;
                    result.ResultType = BrowserResultType.Success;
                }
                else if (options.StartUrl.Contains("/logout") && context.Request.Url.AbsoluteUri == options.EndUrl)
                {
                    displayMessage = SUCCESSFUL_LOGOUT_MESSAGE;
                    result.ResultType = BrowserResultType.Success;
                }
                else
                {
                    displayMessage = ERROR_MESSAGE;
                    result.ResultType = BrowserResultType.UnknownError;
                }

                //return message to be displayed in the browser
                Byte[] buffer = System.Text.Encoding.UTF8.GetBytes(displayMessage);
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.OutputStream.Close();
                context.Response.Close();
                _httpListener.Stop();
            }
        }
        return result;
    }
}