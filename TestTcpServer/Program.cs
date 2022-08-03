using System.Net;
using System.Net.Sockets;
using System.Text;
using Meuzz.Linq.Serialization.Serializers;
using TestClass;

namespace ServerTest
{
    class Program
    {
        static void Main(string[] args)
        {
            TcpListener server = new TcpListener(IPAddress.IPv6Any, 9999);

            server.Start();

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();

                NetworkStream ns = client.GetStream();

                byte[] hello = new byte[100];
                hello = Encoding.Default.GetBytes("hello world");

                ns.Write(hello, 0, hello.Length);

                while (client.Connected)
                {
                    byte[] msg = new byte[1024 * 64];
                    ns.Read(msg, 0, msg.Length);

                    var s = Encoding.UTF8.GetString(msg);

                    var ff = new JsonNetSerializer().Deserialize<Func<SampleItem, bool>>(s);

                    var ret = ff(new SampleItem(1, "hogehoge"));
                    Console.WriteLine(ret); // assumed "True"

                    var ret2 = ff(new SampleItem(2, "fugafuga"));
                    Console.WriteLine(ret2); // assumed "False"

                    client.Close();
                }
            }
        }
    }
}