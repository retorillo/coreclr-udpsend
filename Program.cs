using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;

public class Program
{
  static readonly Encoding encoding = new UTF8Encoding();
  public static int Main(string[] args)
  {
    try {
      if (args.Length < 2) {
        throw new ArgumentException("Usage: dotnet run <hostname> <portnumber>"
          + "\nSet 'localhost' to <hostname> to run as UDP receiver.");
      }
      string host = args[0];
      int port;
      if (!int.TryParse(args[1], out port))
        throw new ArgumentException("invalid port: " + args[1]);
      var canceller = new CancellationTokenSource();
      switch (host) {
        default:
          RunAsClient(host, port, canceller); 
          break;
        case "localhost":
          RunAsServer(port, canceller); 
          break;
      }
      while (Console.Read() == -1) Thread.Sleep(1);
      canceller.Cancel();
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
  static void RunAsClient(string remoteHost, int communicatePort, CancellationTokenSource canceller) {
    var udp = new UdpClient(communicatePort);
    var sendThread = new Thread(() => {
      var remoteIP = Resolve(remoteHost, canceller);
      var remoteEP = new IPEndPoint(remoteIP, communicatePort);
      var c = 0;
      while (!canceller.IsCancellationRequested) {
        var bytes = encoding.GetBytes((c++).ToString());
        var task = udp.SendAsync(bytes, bytes.Length, remoteEP);
        task.Wait(canceller.Token);
        Console.WriteLine("Data sent: " + c);
        Thread.Sleep(1000);
      }
    });
    var echoBackThread = new Thread(() => {
      while (!canceller.IsCancellationRequested) {
        var task = udp.ReceiveAsync();
        task.Wait(canceller.Token);
        var text = encoding.GetString(task.Result.Buffer);
        Console.WriteLine("Data recv :" + text);
        Thread.Sleep(1);
      }
    });
    sendThread.Start();
    echoBackThread.Start();
  }
  static void RunAsServer(int listenPort, CancellationTokenSource canceller) {
    var udp = new UdpClient(listenPort);
    var echoQueue = new Queue<UdpReceiveResult>();
    var listenThread = new Thread(() => {
      Console.WriteLine("Listening...");
      while (!canceller.IsCancellationRequested) {
        var task = udp.ReceiveAsync();
        task.Wait(canceller.Token);
        var text = encoding.GetString(task.Result.Buffer);
        Console.WriteLine("Data recv: " + text + " (" 
          + task.Result.RemoteEndPoint.Address.ToString()
          + ":" + task.Result.RemoteEndPoint.Port + ")");
        echoQueue.Enqueue(task.Result);
        Thread.Sleep(1);
      }
    });
    var echoThread = new Thread(() => {
      while (!canceller.IsCancellationRequested) {
        Thread.Sleep(1);
        if (echoQueue.Count == 0) continue; 
        var result = echoQueue.Dequeue();
        var task = udp.SendAsync(result.Buffer, result.Buffer.Length, result.RemoteEndPoint);
        task.Wait(canceller.Token);
      }
    });
    listenThread.Start();
    echoThread.Start();
  }
}
