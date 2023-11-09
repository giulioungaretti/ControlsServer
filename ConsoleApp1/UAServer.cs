/* ========================================================================

 * ======================================================================*/

using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;

using System.Text;

namespace ControlsServer;

public class UAServer<T> where T : StandardServer, new()
{
    public ApplicationInstance Application { get; private set; }
    public ApplicationConfiguration Configuration => Application.ApplicationConfiguration;

    public bool AutoAccept { get; set; }
    public string Password { get; set; }

    public T? Server { get; private set; }

    /// <summary>
    /// Ctor of the server.
    /// </summary>
    /// <param name="writer">The text output.</param>
    public UAServer(TextWriter writer)
    {
        // TODO: use logging instead of this monstrosity
        m_output = writer;
    }

    /// <summary>
    /// Load the application configuration.
    /// </summary>
    public async Task LoadAsync(string applicationName, string configSectionName)
    {
        try
        {
            ApplicationInstance.MessageDlg = new ApplicationMessageDlg(m_output);
            CertificatePasswordProvider PasswordProvider = new(Password);
            Application = new ApplicationInstance
            {
                ApplicationName = applicationName,
                ApplicationType = ApplicationType.Server,
                ConfigSectionName = configSectionName,
                CertificatePasswordProvider = PasswordProvider
            };

            // load the application configuration.
            _ = await Application.LoadApplicationConfiguration(false).ConfigureAwait(false);

        }
        catch (Exception ex)
        {
            throw new Exception($"Load async error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Load the application configuration.
    /// </summary>
    public async Task CheckCertificateAsync(bool renewCertificate)
    {
        try
        {
            ApplicationConfiguration config = Application.ApplicationConfiguration;
            if (renewCertificate)
            {
                await Application.DeleteApplicationInstanceCertificate().ConfigureAwait(false);
            }

            // check the application certificate.
            bool haveAppCertificate = await Application.CheckApplicationInstanceCertificate(false, minimumKeySize: 0).ConfigureAwait(false);
            if (!haveAppCertificate)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            if (!config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            {
                config.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"CheckCertificateAsync: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Create server instance and add node managers.
    /// </summary>
    public void Create(IList<INodeManagerFactory> nodeManagerFactories)
    {
        try
        {
            // create the server.
            Server = new T();
            if (nodeManagerFactories != null)
            {
                foreach (INodeManagerFactory factory in nodeManagerFactories)
                {
                    Server.AddNodeManager(factory);
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Create Node manager Factories: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Start the server.
    /// </summary>
    public async Task StartAsync()
    {
        try
        {
            // create the server.
            Server ??= new T();

            // start the server
            await Application.Start(Server).ConfigureAwait(false);


            // print endpoint info
            IEnumerable<string> endpoints = Application.Server.GetEndpoints().Select(e => e.EndpointUrl).Distinct();
            foreach (string? endpoint in endpoints)
            {
                m_output.WriteLine(endpoint);
            }

            // start the status thread
            m_status = Task.Run(StatusThreadAsync);

            // print notification on session events
            Server.CurrentInstance.SessionManager.SessionActivated += EventStatus;
            Server.CurrentInstance.SessionManager.SessionClosing += EventStatus;
            Server.CurrentInstance.SessionManager.SessionCreated += EventStatus;
        }
        catch (Exception ex)
        {
            throw new Exception($"Start async: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Stops the server.
    /// </summary>
    public async Task StopAsync()
    {
        try
        {
            if (Server != null)
            {
                using T server = Server;
                // Stop status thread
                Server = null;
                await m_status.ConfigureAwait(false);

                // Stop server and dispose
                server.Stop();
            }

        }
        catch (Exception ex)
        {
            throw new Exception($"Stop async: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The certificate validator is used
    /// if auto accept is not selected in the configuration.
    /// </summary>
    private void CertificateValidator_CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
    {
        if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
        {
            if (AutoAccept)
            {
                m_output.WriteLine("Accepted Certificate: [{0}] [{1}]", e.Certificate.Subject, e.Certificate.Thumbprint);
                e.Accept = true;
                return;
            }
        }
        m_output.WriteLine("Rejected Certificate: {0} [{1}] [{2}]", e.Error, e.Certificate.Subject, e.Certificate.Thumbprint);
    }

    /// <summary>
    /// Update the session status.
    /// </summary>
    private void EventStatus(Session session, SessionEventReason reason)
    {
        m_lastEventTime = DateTime.UtcNow;
        PrintSessionStatus(session, reason.ToString());
    }

    /// <summary>
    /// Output the status of a connected session.
    /// </summary>
    private void PrintSessionStatus(Session session, string reason, bool lastContact = false)
    {
        StringBuilder item = new();
        lock (session.DiagnosticsLock)
        {
            _ = item.AppendFormat("{0,9}:{1,20}:", reason, session.SessionDiagnostics.SessionName);
            if (lastContact)
            {
                _ = item.AppendFormat("Last Event:{0:HH:mm:ss}", session.SessionDiagnostics.ClientLastContactTime.ToLocalTime());
            }
            else
            {
                if (session.Identity != null)
                {
                    _ = item.AppendFormat(":{0,20}", session.Identity.DisplayName);
                }
                _ = item.AppendFormat(":{0}", session.Id);
            }
        }
        m_output.WriteLine(item.ToString());
    }

    /// <summary>
    /// Status thread, prints connection status every 10 seconds.
    /// </summary>
    private async Task StatusThreadAsync()
    {
        while (Server != null)
        {
            if (DateTime.UtcNow - m_lastEventTime > TimeSpan.FromMilliseconds(10000))
            {
                IList<Session> sessions = Server.CurrentInstance.SessionManager.GetSessions();
                for (int ii = 0; ii < sessions.Count; ii++)
                {
                    Session session = sessions[ii];
                    PrintSessionStatus(session, "-Status-", true);
                }
                m_lastEventTime = DateTime.UtcNow;
            }
            await Task.Delay(1000).ConfigureAwait(false);
        }
    }

    #region Private Members
    private readonly TextWriter m_output;
    private Task m_status;
    private DateTime m_lastEventTime;
    #endregion
}

