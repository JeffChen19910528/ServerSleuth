using ServerSleuth.Linux.Configuration;

namespace ServerSleuth.Linux.Tests.Configuration;

public class LinuxConfigurationTechnologyAnalyzerTests
{
    [Fact]
    public void Analyze_SystemdUnit_ExtractsExecStartUserWorkingDirectoryAndDependencies()
    {
        const string unit = """
            [Unit]
            Description=ERP Web Service
            After=network.target
            Requires=postgresql.service
            Wants=redis.service

            [Service]
            ExecStart=/opt/erp/bin/erp --port=8080
            User=erp
            WorkingDirectory=/opt/erp
            Restart=on-failure
            EnvironmentFile=/etc/erp/erp.env
            """;

        var facts = LinuxConfigurationTechnologyAnalyzer.Analyze("Systemd", unit);

        Assert.Equal("ERP Web Service", facts["Description"]);
        Assert.Equal("/opt/erp/bin/erp --port=8080", facts["ExecStart"]);
        Assert.Equal("erp", facts["User"]);
        Assert.Equal("/opt/erp", facts["WorkingDirectory"]);
        Assert.Equal("on-failure", facts["Restart"]);
        Assert.Equal("network.target", facts["After"]);
        Assert.Equal("postgresql.service", facts["Requires"]);
        Assert.Equal("redis.service", facts["Wants"]);
        Assert.Equal("/etc/erp/erp.env", facts["EnvironmentFile0"]);
    }

    [Fact]
    public void Analyze_SystemdUnit_MultipleEnvironmentFileLines_AllCaptured_NoneLost()
    {
        const string unit = """
            [Service]
            EnvironmentFile=/etc/erp/common.env
            EnvironmentFile=-/etc/erp/optional.env
            """;

        var facts = LinuxConfigurationTechnologyAnalyzer.Analyze("Systemd", unit);

        Assert.Equal("/etc/erp/common.env", facts["EnvironmentFile0"]);
        Assert.Equal("/etc/erp/optional.env", facts["EnvironmentFile1"]); // leading "-" (ignore-if-missing marker) stripped, never treated as part of the path
    }

    [Fact]
    public void Analyze_ApplicationRootSourceWithoutUnitSections_ProducesNoSystemdFacts()
    {
        var facts = LinuxConfigurationTechnologyAnalyzer.Analyze("ApplicationRoot", "{ \"key\": \"value\" }");

        Assert.Empty(facts);
    }

    [Fact]
    public void Analyze_Nginx_ExtractsServerBlockDirectivesAndUpstream()
    {
        const string conf = """
            upstream erp_backend {
                server 127.0.0.1:9000;
            }
            server {
                listen 443 ssl;
                server_name erp.example.com;
                root /var/www/erp;
                include /etc/nginx/conf.d/erp-common.conf;
                location /api/ {
                    proxy_pass http://erp_backend;
                }
            }
            """;

        var facts = LinuxConfigurationTechnologyAnalyzer.Analyze("Nginx", conf);

        Assert.Equal("443 ssl", facts["listen0"]);
        Assert.Equal("erp.example.com", facts["server_name0"]);
        Assert.Equal("/var/www/erp", facts["root0"]);
        Assert.Equal("http://erp_backend", facts["proxy_pass0"]);
        Assert.Equal("erp_backend", facts["upstreams"]);
    }

    [Fact]
    public void Analyze_Apache_ExtractsVirtualHostAndDirectives()
    {
        const string conf = """
            Listen 80
            <VirtualHost *:80>
                ServerName erp.example.com
                DocumentRoot /var/www/erp
                ProxyPass /api http://127.0.0.1:9000/
                Include /etc/apache2/conf-available/erp.conf
            </VirtualHost>
            """;

        var facts = LinuxConfigurationTechnologyAnalyzer.Analyze("Apache", conf);

        Assert.Equal("*:80", facts["VirtualHost0"]);
        Assert.Equal("80", facts["Listen0"]);
        Assert.Equal("erp.example.com", facts["ServerName0"]);
        Assert.Equal("/var/www/erp", facts["DocumentRoot0"]);
        Assert.Equal("/api", facts["ProxyPass0"]);
    }

    [Fact]
    public void Analyze_Php_ExtractsReferencedExtensions()
    {
        const string ini = """
            extension=mysqli
            zend_extension=opcache
            """;

        var facts = LinuxConfigurationTechnologyAnalyzer.Analyze("Php", ini);

        Assert.Equal("mysqli,opcache", facts["Extensions"]);
    }

    [Fact]
    public void Analyze_MySql_ExtractsDatadirSocketPortBindAddress()
    {
        const string cnf = """
            [mysqld]
            datadir=/var/lib/mysql
            socket=/var/run/mysqld/mysqld.sock
            port=3306
            bind-address=127.0.0.1
            """;

        var facts = LinuxConfigurationTechnologyAnalyzer.Analyze("MySql", cnf);

        Assert.Equal("/var/lib/mysql", facts["datadir"]);
        Assert.Equal("/var/run/mysqld/mysqld.sock", facts["socket"]);
        Assert.Equal("3306", facts["port"]);
        Assert.Equal("127.0.0.1", facts["bind-address"]);
    }

    [Fact]
    public void Analyze_PostgreSql_ExtractsPortListenAddressesDataDirectory()
    {
        const string conf = """
            port = 5432
            listen_addresses = 'localhost'
            data_directory = '/var/lib/postgresql/16/main'
            # a comment line
            include = 'extra.conf'
            """;

        var facts = LinuxConfigurationTechnologyAnalyzer.Analyze("PostgreSql", conf);

        Assert.Equal("5432", facts["port"]);
        Assert.Equal("localhost", facts["listen_addresses"]);
        Assert.Equal("/var/lib/postgresql/16/main", facts["data_directory"]);
        Assert.Equal("extra.conf", facts["include"]);
    }

    [Fact]
    public void Analyze_Ssh_ExtractsPortListenAddressIncludeAndHostKeyPath_NeverReadsKeyContent()
    {
        const string sshdConfig = """
            Port 2222
            ListenAddress 0.0.0.0
            Include /etc/ssh/sshd_config.d/*.conf
            HostKey /etc/ssh/ssh_host_rsa_key
            HostKey /etc/ssh/ssh_host_ed25519_key
            """;

        var facts = LinuxConfigurationTechnologyAnalyzer.Analyze("Ssh", sshdConfig);

        Assert.Equal("2222", facts["Port0"]);
        Assert.Equal("0.0.0.0", facts["ListenAddress0"]);
        Assert.Equal("/etc/ssh/sshd_config.d/*.conf", facts["Include0"]);
        Assert.Equal("/etc/ssh/ssh_host_rsa_key", facts["HostKey0"]);
        Assert.Equal("/etc/ssh/ssh_host_ed25519_key", facts["HostKey1"]);
        Assert.DoesNotContain(facts.Values, v => v.Contains("PRIVATE KEY"));
    }

    [Fact]
    public void Analyze_UnrecognizedSource_ReturnsEmptyFacts()
    {
        var facts = LinuxConfigurationTechnologyAnalyzer.Analyze("SomeUnknownSource", "arbitrary text");

        Assert.Empty(facts);
    }
}
