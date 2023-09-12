using Ocelot.Configuration.File;

namespace Ocelot.Configuration.Creator;

public interface IClientCertificateOptionsCreator
{
    ClientCertificateOptions Create(FileClientCertificateOptions fileRouteFileClientCertificateOptions);
}