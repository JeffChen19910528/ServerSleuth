using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;
using ServerSleuth.Infrastructure.Common;

namespace ServerSleuth.Windows.Remote;

/// <summary>
/// The only real <see cref="ICimSession"/> implementation — wraps
/// <c>Microsoft.Management.Infrastructure.CimSession</c>, the same native CIM/WS-Man client
/// library PowerShell's own <c>Get-CimInstance -CimSession</c>/<c>Invoke-CimMethod</c> cmdlets
/// are built on (skill.md Phase 10D-3B §3: "if the selected library internally uses WS-Man SOAP
/// requests, that is acceptable"). No PowerShell script engine (<c>System.Management.Automation</c>)
/// is referenced anywhere in this codebase — this library speaks the WS-Management SOAP
/// protocol directly and returns typed <c>CimInstance</c>/<c>CimMethodResult</c> objects, never
/// script text.
///
/// **TLS/server-validation guarantee (skill.md §7) — hard-coded, not configurable, verified by
/// <c>NoServerCertificateBypassTests</c>**: whenever <see cref="WinRmConnectionOptions.UseSsl"/>
/// is <c>true</c>, <c>CertCACheck</c>/<c>CertCNCheck</c>/<c>CertRevocationCheck</c> are ALWAYS
/// set to <c>true</c> below — there is no field, flag, or code path anywhere in this class that
/// can set any of them to <c>false</c>. <c>NoEncryption</c> is ALWAYS <c>false</c> (message-level
/// encryption is mandatory even on the non-TLS port) — never exposed as a setting at all.
/// </summary>
public sealed class CimNetSession(WinRmConnectionOptions connectionOptions, IWindowsRemoteCredentialProvider credentialProvider) : ICimSession
{
    private CimSession? _session;

    public void Connect(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var credential = credentialProvider.GetCredential();

        var options = new WSManSessionOptions
        {
            DestinationPort = (uint)connectionOptions.ResolvedPort,
            UseSsl = connectionOptions.UseSsl,
            NoEncryption = false, // never configurable — see type doc comment.
            CertCACheck = connectionOptions.UseSsl,
            CertCNCheck = connectionOptions.UseSsl,
            CertRevocationCheck = connectionOptions.UseSsl
        };

        var mechanism = credential.AuthenticationMechanism switch
        {
            WindowsRemoteAuthenticationMechanism.Kerberos => PasswordAuthenticationMechanism.Kerberos,
            WindowsRemoteAuthenticationMechanism.CredSsp => PasswordAuthenticationMechanism.CredSsp,
            _ => PasswordAuthenticationMechanism.Negotiate
        };

        options.AddDestinationCredentials(new CimCredential(mechanism, credential.Domain, credential.UserName, credential.Password));

        try
        {
            using (var sessionOptions = options)
            {
                sessionOptions.Timeout = connectionOptions.ConnectTimeout;
                _session = CimSession.Create(connectionOptions.Host, sessionOptions);
            }

            // CimSession.Create alone does NOT perform an eager network handshake — a WS-Man
            // CIM session is lazily connected on its first real operation (unlike SSH.NET's
            // SshClient.Connect(), which IS eager). Verified against a real, guaranteed-closed
            // port during Phase 10D-3B's own real-network-stack acceptance check: without this
            // TestConnection() call, Create() alone returned successfully even against an
            // unreachable target, and the actual failure only surfaced later as scanner-level
            // PartiallySupported statuses rather than an upfront connect failure — see
            // ARCHITECTURE.md's Phase 10D-3B addendum. Calling TestConnection() here forces the
            // real round trip eagerly, matching skill.md §8's "never connect until the scan
            // begins, but DO fail cleanly at that point" expectation.
            if (!_session.TestConnection(out _, out var testConnectionException))
            {
                throw testConnectionException ?? new CimException("TestConnection failed with no further detail.");
            }
        }
        catch (CimException ex)
        {
            throw new WinRmConnectException(ClassifyConnectFailure(ex), ex.Message, ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new WinRmConnectException(OperationStatus.TransportUnavailable, ex.Message, ex);
        }
    }

    private static OperationStatus ClassifyConnectFailure(CimException ex) => ex.NativeErrorCode switch
    {
        NativeErrorCode.AccessDenied => OperationStatus.AccessDenied,
        NativeErrorCode.InvalidOperationTimeout => OperationStatus.Timeout,
        _ => OperationStatus.TransportUnavailable
    };

    public CimQueryOutcome QueryInstances(string ns, string wqlQuery, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            return CimQueryOutcome.Failure(OperationStatus.TransportUnavailable, "Not connected.");
        }

        try
        {
            using var operationOptions = new CimOperationOptions { Timeout = timeout, CancellationToken = cancellationToken };
            var rows = new List<IReadOnlyDictionary<string, object?>>();

            foreach (var instance in _session.QueryInstances(ns, "WQL", wqlQuery, operationOptions))
            {
                using (instance)
                {
                    rows.Add(ToPropertyMap(instance));
                }
            }

            return CimQueryOutcome.Ok(rows);
        }
        catch (OperationCanceledException)
        {
            return CimQueryOutcome.Failure(OperationStatus.Cancelled);
        }
        catch (CimException ex)
        {
            return CimQueryOutcome.Failure(ClassifyOperationFailure(ex), ex.Message);
        }
        catch (Exception ex)
        {
            return CimQueryOutcome.Failure(OperationStatus.TransportUnavailable, ex.Message);
        }
    }

    public CimMethodOutcome InvokeMethod(
        string ns, string className, IReadOnlyDictionary<string, object?>? instanceKeyProperties,
        string methodName, IReadOnlyDictionary<string, object?> parameters, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!WindowsWmiMethodAllowList.IsAllowed(ns, className, methodName))
        {
            return CimMethodOutcome.Failure(OperationStatus.InvalidInput, $"'{ns}!{className}.{methodName}' is not on the allow-list.");
        }

        if (_session is null)
        {
            return CimMethodOutcome.Failure(OperationStatus.TransportUnavailable, "Not connected.");
        }

        try
        {
            using var operationOptions = new CimOperationOptions { Timeout = timeout, CancellationToken = cancellationToken };
            using var methodParameters = new CimMethodParametersCollection();
            foreach (var (name, value) in parameters)
            {
                methodParameters.Add(CimMethodParameter.Create(name, value, CimFlags.None));
            }

            CimMethodResult result;
            if (instanceKeyProperties is null)
            {
                result = _session.InvokeMethod(ns, className, methodName, methodParameters, operationOptions);
            }
            else
            {
                using var instance = new CimInstance(className, ns);
                foreach (var (name, value) in instanceKeyProperties)
                {
                    instance.CimInstanceProperties.Add(CimProperty.Create(name, value, CimFlags.Key));
                }

                result = _session.InvokeMethod(ns, instance, methodName, methodParameters, operationOptions);
            }

            using (result)
            {
                var returnValue = result.ReturnValue?.Value is IConvertible convertible ? Convert.ToUInt32(convertible) : (uint?)null;
                var outParameters = result.OutParameters.ToDictionary(p => p.Name, object? (p) => p.Value);
                return CimMethodOutcome.Ok(returnValue ?? 0u, outParameters);
            }
        }
        catch (OperationCanceledException)
        {
            return CimMethodOutcome.Failure(OperationStatus.Cancelled);
        }
        catch (CimException ex)
        {
            return CimMethodOutcome.Failure(ClassifyOperationFailure(ex), ex.Message);
        }
        catch (Exception ex)
        {
            return CimMethodOutcome.Failure(OperationStatus.TransportUnavailable, ex.Message);
        }
    }

    private static OperationStatus ClassifyOperationFailure(CimException ex) => ex.NativeErrorCode switch
    {
        NativeErrorCode.AccessDenied => OperationStatus.AccessDenied,
        NativeErrorCode.InvalidOperationTimeout => OperationStatus.Timeout,
        NativeErrorCode.NotFound => OperationStatus.NotFound,
        NativeErrorCode.InvalidNamespace or NativeErrorCode.InvalidClass or NativeErrorCode.NotSupported
            or NativeErrorCode.MethodNotFound or NativeErrorCode.MethodNotAvailable => OperationStatus.NotInstalled,
        NativeErrorCode.InvalidQuery or NativeErrorCode.QueryLanguageNotSupported or NativeErrorCode.InvalidParameter => OperationStatus.ProtocolError,
        _ => OperationStatus.ExecutionFailed
    };

    private static IReadOnlyDictionary<string, object?> ToPropertyMap(CimInstance instance)
    {
        var map = new Dictionary<string, object?>();
        foreach (var property in instance.CimInstanceProperties)
        {
            map[property.Name] = property.Value;
        }

        return map;
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}

/// <summary>Thrown only by <see cref="ICimSession.Connect"/> — a connection failure is a
/// distinct, up-front event (caught once by <see cref="CimWinRmTransport.Connect"/>), never
/// something a per-operation caller needs to catch mid-scan.</summary>
public sealed class WinRmConnectException(OperationStatus status, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public OperationStatus Status { get; } = status;
}
