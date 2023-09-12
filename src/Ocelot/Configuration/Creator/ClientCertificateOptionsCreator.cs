using Ocelot.Configuration.File;

namespace Ocelot.Configuration.Creator;

public class ClientCertificateOptionsCreator : IClientCertificateOptionsCreator
{
    public ClientCertificateOptions Create(FileClientCertificateOptions fileRouteFileClientCertificateOptions)
    {
        return new ClientCertificateOptions
        {
            Store = fileRouteFileClientCertificateOptions.Store,
            Location = fileRouteFileClientCertificateOptions.Location,
            Thumbprint = fileRouteFileClientCertificateOptions.Thumbprint,
        };
    }
}