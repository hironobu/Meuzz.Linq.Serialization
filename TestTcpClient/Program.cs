using System.Net;
using System.Net.Sockets;
using System.Text;
using Meuzz.Linq.Serialization.Serializers;
using TestClass;

namespace TcpClient
{
    class Client
    {
        private static void Main(String[] args)
        {
            Thread.Sleep(3000);

            IPHostEntry iphostInfo = Dns.GetHostEntry("localhost");
            IPAddress ipAddress = iphostInfo.AddressList[0];
            IPEndPoint ipEndpoint = new IPEndPoint(ipAddress, 9999);

            Socket client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                client.Connect(ipEndpoint);

                Console.WriteLine("Socket created to {0}", client.RemoteEndPoint?.ToString());

                var ss = "hogehoge";
                var s = JsonNetSerializer.Serialize<Func<SampleItem, bool>>(x => x.Name == ss);
                var data = Encoding.UTF8.GetBytes((string)s);

                int n = client.Send(data);

                var buf = new byte[1024 * 64];
                int m = client.Receive(buf);

                Console.WriteLine("" + Encoding.ASCII.GetString(data));
                client.Shutdown(SocketShutdown.Both);
                client.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine("Transmission end.");
            Console.ReadKey();
        }
    }
}