using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class Program
{
  static readonly Encoding encoding = new UTF8Encoding();
  public static int Main(string[] args)
  {
    try {
      if (args.Length < 2) {
        throw new ArgumentException("   Usage: dotnet run <hostname> <portnumber>"
          + "\n   Set '*' or 'localhost' to <hostname> to run as UDP receiver.");
        return 1;
      }
      string host = args[0];
      int port;
      if (!int.TryParse(args[1], out port))
        throw new ArgumentException("invalid port: " + args[1]);
      var canceller = new CancellationTokenSource();
      switch (host) {
        default:
          RunAsSender(host, port, canceller); 
          break;
        case "localhost": goto case "*";
        case "*": 
          RunAsReceiver(port, canceller); 
          break;
      }
    }
    catch (SocketException e) {
      Console.WriteLine(e.Message);
      Console.WriteLine();
      return 1;
    }
    catch (ArgumentException e) {
      Console.WriteLine(e.Message);
      Console.WriteLine();
      return 1;
    }
    return 0;
  }
  static IPAddress Resolve(string hostname, CancellationTokenSource canceller) {
    var task = Dns.GetHostEntryAsync(hostname);
    task.Wait(canceller.Token);
    var ip = task.Result;
    return ip.AddressList[0];
  }
  static void RunAsSender(string remoteHost, int remotePort, CancellationTokenSource canceller) {
    var udp = new UdpClient();
    var remoteIP = Resolve(remoteHost, canceller);
    var remoteEP = new IPEndPoint(remoteIP, remotePort);
    var c = 0;
    while (!canceller.IsCancellationRequested) {
      var bytes = encoding.GetBytes((c++).ToString());
      var task = udp.SendAsync(bytes, bytes.Length, remoteEP);
      task.Wait(canceller.Token);
      Console.WriteLine("Data sent: " + c);
      Thread.Sleep(1000);
    }
  }
  static void RunAsReceiver(int listenPort, CancellationTokenSource canceller) {
    var udp = new UdpClient(listenPort);
    while (!canceller.IsCancellationRequested) {
      Console.WriteLine("Listening...");
      var task = udp.ReceiveAsync();
      task.Wait(canceller.Token);
      var bytes = task.Result.Buffer;
      var text = encoding.GetString(bytes);
      Console.WriteLine("Data recv:" + text);
      System.Threading.Thread.Sleep(1);
    }
  }
}
