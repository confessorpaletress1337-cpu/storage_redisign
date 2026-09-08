using Duende.IdentityModel.OidcClient.Browser;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace StorageClient.Redesign.Services;

public sealed class SystemBrowser : IBrowser
{
    private readonly string _redirectUri;

    public SystemBrowser(string redirectUri)
    {
        _redirectUri = redirectUri.EndsWith('/') ? redirectUri : redirectUri + "/";
    }

    public async Task<BrowserResult> InvokeAsync(
        BrowserOptions options,
        CancellationToken cancellationToken = default)
    {
        using HttpListener listener = new();
        listener.Prefixes.Add(_redirectUri);
        listener.Start();

        Process.Start(new ProcessStartInfo
        {
            FileName = options.StartUrl,
            UseShellExecute = true
        });

        try
        {
            Task<HttpListenerContext> contextTask = listener.GetContextAsync();
            Task completedTask = await Task.WhenAny(
                contextTask,
                Task.Delay(options.Timeout, cancellationToken));

            if (completedTask != contextTask)
            {
                return new BrowserResult
                {
                    ResultType = cancellationToken.IsCancellationRequested
                        ? BrowserResultType.UserCancel
                        : BrowserResultType.Timeout
                };
            }

            HttpListenerContext context = await contextTask;
            await WriteBrowserResponseAsync(context.Response);

            return new BrowserResult
            {
                ResultType = BrowserResultType.Success,
                Response = context.Request.Url?.AbsoluteUri
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new BrowserResult
            {
                ResultType = BrowserResultType.UnknownError,
                Error = exception.Message
            };
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task WriteBrowserResponseAsync(HttpListenerResponse response)
    {
        const string html = """
            <!doctype html>
            <html lang="ru">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Вход выполнен</title>
                <style>
                    body { margin:0; min-height:100vh; display:grid; place-items:center; font-family:Segoe UI,sans-serif; background:#eef3f7; color:#22303a; }
                    main { max-width:460px; margin:24px; padding:32px; border-radius:24px; background:#fff; box-shadow:0 18px 50px #1b667322; }
                    h1 { margin:0 0 12px; color:#176c7a; }
                    p { margin:0; line-height:1.6; color:#687683; }
                </style>
            </head>
            <body>
                <main>
                    <h1>Вход выполнен</h1>
                    <p>Можно закрыть эту вкладку и вернуться в приложение склада.</p>
                </main>
                <script>setTimeout(() => window.close(), 900);</script>
            </body>
            </html>
            """;

        byte[] buffer = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;
        response.ContentType = "text/html; charset=utf-8";
        await response.OutputStream.WriteAsync(buffer);
        response.OutputStream.Close();
    }
}
