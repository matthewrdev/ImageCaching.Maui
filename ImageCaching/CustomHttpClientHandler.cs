using System;
namespace ImageCaching
{
	public class CustomHttpClientHandler : HttpClientHandler
	{
		public CustomHttpClientHandler()
		{
		}

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return base.SendAsync(request, cancellationToken);
        }
    }
}

