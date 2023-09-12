using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Ocelot.Configuration;

using Ocelot.Logging;

namespace Ocelot.Requester
{
    public class HttpClientBuilder : IHttpClientBuilder
    {
        private readonly IDelegatingHandlerHandlerFactory _factory;
        private readonly IHttpClientCache _cacheHandlers;
        private readonly IOcelotLogger _logger;
        private DownstreamRoute _cacheKey;
        private HttpClient _httpClient;
        private IHttpClient _client;
        private readonly TimeSpan _defaultTimeout;

        public HttpClientBuilder(
            IDelegatingHandlerHandlerFactory factory,
            IHttpClientCache cacheHandlers,
            IOcelotLogger logger)
        {
            _factory = factory;
            _cacheHandlers = cacheHandlers;
            _logger = logger;

            // This is hardcoded at the moment but can easily be added to configuration
            // if required by a user request.
            _defaultTimeout = TimeSpan.FromSeconds(90);
        }

        public IHttpClient Create(DownstreamRoute downstreamRoute)
        {
            _cacheKey = downstreamRoute;

            var httpClient = _cacheHandlers.Get(_cacheKey);

            if (httpClient != null)
            {
                _client = httpClient;
                return httpClient;
            }

            var handler = CreateHandler(downstreamRoute);

            if (downstreamRoute.DangerousAcceptAnyServerCertificateValidator)
            {
                handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                _logger
                    .LogWarning($"You have ignored all SSL warnings by using DangerousAcceptAnyServerCertificateValidator for this DownstreamRoute, UpstreamPathTemplate: {downstreamRoute.UpstreamPathTemplate}, DownstreamPathTemplate: {downstreamRoute.DownstreamPathTemplate}");
            }

            var timeout = downstreamRoute.QosOptions.TimeoutValue == 0
                ? _defaultTimeout
                : TimeSpan.FromMilliseconds(downstreamRoute.QosOptions.TimeoutValue);

            _httpClient = new HttpClient(CreateHttpMessageHandler(handler, downstreamRoute))
            {
                Timeout = timeout,
            };

            _client = new HttpClientWrapper(_httpClient);

            return _client;
        }

        private static HttpClientHandler CreateHandler(DownstreamRoute downstreamRoute)
        {
            // Dont' create the CookieContainer if UseCookies is not set or the HttpClient will complain
            // under .Net Full Framework
            var useCookies = downstreamRoute.HttpHandlerOptions.UseCookieContainer;

            return useCookies ? UseCookiesHandler(downstreamRoute) : UseNonCookiesHandler(downstreamRoute);
        }

        private static HttpClientHandler UseNonCookiesHandler(DownstreamRoute downstreamRoute)
        {
            var clientHandler = new HttpClientHandler
            {
                AllowAutoRedirect = downstreamRoute.HttpHandlerOptions.AllowAutoRedirect,
                UseCookies = downstreamRoute.HttpHandlerOptions.UseCookieContainer,
                UseProxy = downstreamRoute.HttpHandlerOptions.UseProxy,
                MaxConnectionsPerServer = downstreamRoute.HttpHandlerOptions.MaxConnectionsPerServer,
            };

            if (string.IsNullOrEmpty(downstreamRoute.ClientCertificateOptions.Store) ||
                string.IsNullOrEmpty(downstreamRoute.ClientCertificateOptions.Location) ||
                string.IsNullOrEmpty(downstreamRoute.ClientCertificateOptions.Thumbprint) ||
                !Enum.TryParse(downstreamRoute.ClientCertificateOptions.Store, out StoreName storeName) ||
                !Enum.TryParse(downstreamRoute.ClientCertificateOptions.Location, out StoreLocation storeLocation))
            {
                return clientHandler;
            }

            var store = new X509Store(storeName, storeLocation);

            store.Open(OpenFlags.ReadOnly);

            var certificate = store.Certificates.First(cert =>
                cert.Thumbprint == downstreamRoute.ClientCertificateOptions.Thumbprint);

            clientHandler.ClientCertificates.Add(certificate);

            return clientHandler;
        }

        private static HttpClientHandler UseCookiesHandler(DownstreamRoute downstreamRoute)
        {
            var clientHandler = new HttpClientHandler
            {
                AllowAutoRedirect = downstreamRoute.HttpHandlerOptions.AllowAutoRedirect,
                UseCookies = downstreamRoute.HttpHandlerOptions.UseCookieContainer,
                UseProxy = downstreamRoute.HttpHandlerOptions.UseProxy,
                MaxConnectionsPerServer = downstreamRoute.HttpHandlerOptions.MaxConnectionsPerServer,
                CookieContainer = new CookieContainer(),
            };

            if (string.IsNullOrEmpty(downstreamRoute.ClientCertificateOptions.Store) ||
                string.IsNullOrEmpty(downstreamRoute.ClientCertificateOptions.Location) ||
                string.IsNullOrEmpty(downstreamRoute.ClientCertificateOptions.Thumbprint) ||
                !Enum.TryParse(downstreamRoute.ClientCertificateOptions.Store, out StoreName storeName) ||
                !Enum.TryParse(downstreamRoute.ClientCertificateOptions.Location, out StoreLocation storeLocation))
            {
                return clientHandler;
            }

            var store = new X509Store(storeName, storeLocation);

            store.Open(OpenFlags.ReadOnly);

            var certificate = store.Certificates.First(cert =>
                cert.Thumbprint == downstreamRoute.ClientCertificateOptions.Thumbprint);

            clientHandler.ClientCertificates.Add(certificate);

            return clientHandler;
        }

        public void Save()
        {
            _cacheHandlers.Set(_cacheKey, _client, TimeSpan.FromHours(24));
        }

        private HttpMessageHandler CreateHttpMessageHandler(HttpMessageHandler httpMessageHandler, DownstreamRoute request)
        {
            //todo handle error
            var handlers = _factory.Get(request).Data;

            handlers
                .Select(handler => handler)
                .Reverse()
                .ToList()
                .ForEach(handler =>
                {
                    var delegatingHandler = handler();
                    delegatingHandler.InnerHandler = httpMessageHandler;
                    httpMessageHandler = delegatingHandler;
                });
            return httpMessageHandler;
        }
    }
}
