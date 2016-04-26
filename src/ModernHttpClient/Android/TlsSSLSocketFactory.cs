using Javax.Net.Ssl;

namespace ModernHttpClient
{

    public class TlsSSLSocketFactory : SSLSocketFactory
    {
        readonly SSLSocketFactory factory = (SSLSocketFactory)Default;

        public override string[] GetDefaultCipherSuites()
        {
            return factory.GetDefaultCipherSuites();
        }

        public override string[] GetSupportedCipherSuites()
        {
            return factory.GetSupportedCipherSuites();
        }
        public override Java.Net.Socket CreateSocket(Java.Net.InetAddress address, int port, Java.Net.InetAddress localAddress, int localPort)
        {
            SSLSocket socket = (SSLSocket)factory.CreateSocket(address, port, localAddress, localPort);
            socket.SetEnabledProtocols(socket.GetSupportedProtocols());
            socket.SetEnabledCipherSuites(socket.GetSupportedCipherSuites());

            return socket;
        }

        public override Java.Net.Socket CreateSocket(Java.Net.InetAddress host, int port)
        {
            SSLSocket socket = (SSLSocket)factory.CreateSocket(host, port);
            socket.SetEnabledProtocols(socket.GetSupportedProtocols());
            socket.SetEnabledCipherSuites(socket.GetSupportedCipherSuites());

            return socket;
        }

        public override Java.Net.Socket CreateSocket(string host, int port, Java.Net.InetAddress localHost, int localPort)
        {
            SSLSocket socket = (SSLSocket)factory.CreateSocket(host, port, localHost, localPort);
            socket.SetEnabledProtocols(socket.GetSupportedProtocols());
            socket.SetEnabledCipherSuites(socket.GetSupportedCipherSuites());

            return socket;
        }

        public override Java.Net.Socket CreateSocket(string host, int port)
        {
            SSLSocket socket = (SSLSocket)factory.CreateSocket(host, port);
            socket.SetEnabledProtocols(socket.GetSupportedProtocols());
            socket.SetEnabledCipherSuites(socket.GetSupportedCipherSuites());

            return socket;
        }

        public override Java.Net.Socket CreateSocket(Java.Net.Socket s, string host, int port, bool autoClose)
        {
            SSLSocket socket = (SSLSocket)factory.CreateSocket(s, host, port, autoClose);
            socket.SetEnabledProtocols(socket.GetSupportedProtocols());
            socket.SetEnabledCipherSuites(socket.GetSupportedCipherSuites());

            return socket;
        }

        protected override void Dispose(bool disposing)
        {
            factory.Dispose();
            base.Dispose(disposing);
        }

        public override Java.Net.Socket CreateSocket()
        {
            SSLSocket socket = (SSLSocket)factory.CreateSocket();
            socket.SetEnabledProtocols(socket.GetSupportedProtocols());
            socket.SetEnabledCipherSuites(socket.GetSupportedCipherSuites());

            return socket;
        }
    }
}