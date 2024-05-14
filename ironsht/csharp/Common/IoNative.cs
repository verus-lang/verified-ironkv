using IronfleetIoFramework;
using System;
using System.Net.Sockets;
using System.Numerics;
using System.Diagnostics;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FStream = System.IO.FileStream;

namespace IoNative {

  public partial class PrintParams
  {
    internal static bool shouldPrintProfilingInfo = false;
    internal static bool shouldPrintProgress = false;

    public static bool ShouldPrintProfilingInfo() { return shouldPrintProfilingInfo; }
    public static bool ShouldPrintProgress() { return shouldPrintProgress; }

    public static void SetParameters(bool i_shouldPrintProfilingInfo, bool i_shouldPrintProgress)
    {
      shouldPrintProfilingInfo = i_shouldPrintProfilingInfo;
      shouldPrintProgress = i_shouldPrintProgress;
    }
  }
  
  public class NetClient<BufferType>
  {
    internal IoScheduler<BufferType> scheduler;

    byte[] myPublicKeyHash;

    internal NetClient(IoScheduler<BufferType> i_scheduler, byte[] publicKey)
    {
      scheduler = i_scheduler;
      myPublicKeyHash = scheduler.HashPublicKey(publicKey);
    }

    public static int MaxPublicKeySize { get { return 0xFFFFF; } }

    public byte[] MyPublicKey() { return myPublicKeyHash; }

    public static NetClient<BufferType> Create(PrivateIdentity myIdentity, string localHostNameOrAddress, int localPort,
                                              List<PublicIdentity> knownIdentities, BufferManager<BufferType> bufferManager,
                                              bool verbose, bool useSsl, int maxSendRetries = 3)
    {
      try
      {
        var scheduler = IoScheduler<BufferType>.CreateServer(myIdentity, localHostNameOrAddress, localPort, knownIdentities,
                                                             bufferManager, verbose, useSsl, maxSendRetries);
        var myPublicKey = IronfleetCrypto.GetCertificatePublicKey(scheduler.MyCert);
        if (myPublicKey.Length > MaxPublicKeySize) {
          System.Console.Error.WriteLine("ERROR:  The provided public key for my identity is too big ({0} > {1} bytes)",
                                         myPublicKey.Length, MaxPublicKeySize);
          return null;
        }
        return new NetClient<BufferType>(scheduler, myPublicKey);
      }
      catch (Exception e)
      {
        System.Console.Error.WriteLine(e);
        return null;
      }
    }
  
    public void Receive(int timeLimit, out bool ok, out bool timedOut, out byte[] remote, out Option<BufferType> buffer)
    {
      scheduler.ReceivePacket(timeLimit, out ok, out timedOut, out remote, out buffer);
      if (ok && !timedOut && remote != null && remote.Length > MaxPublicKeySize) {
        ok = false;
      }
    }
  
    public bool Send(Span<byte> remote, Span<byte> buffer)
    {
      return scheduler.SendPacket(remote, buffer);
    }

    public byte[] HashPublicKey(byte[] key)
    {
      return scheduler.HashPublicKey(key);
    }
  }
  
  public class Time
  {
    static Stopwatch watch;
  
    public static void Initialize()
    {
      watch = new Stopwatch();
      watch.Start();
    }
  
    public static ulong GetTime()
    {
      return (ulong) (DateTime.Now.Ticks / 10000);
    }
      
    public static ulong GetDebugTimeTicks()
    {
      return (ulong) watch.ElapsedTicks;
    }
      
    public static void RecordTiming(char[] name, ulong time)
    {
      var str = new string(name);
      IronfleetCommon.Profiler.Record(str, (long)time);
    }
  }
}

