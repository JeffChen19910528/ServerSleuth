namespace ServerSleuth.Windows.Certificates;

public interface ICertificateStoreProvider
{
    /// <summary>Reads every certificate in the given store. Never opens the store for write,
    /// never exports/reads private key material.</summary>
    CertificateStoreReadResult ReadStore(CertificateStoreSource source);
}
