using IronfleetCommon;
using IronfleetIoFramework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Diagnostics;

namespace IronSHTClient
{
  // Analog of CSingleMessage
  public abstract class MessageBase
  {
    protected ulong seqno;
    protected MessageBase(ulong seqno)
    {
      this.seqno = seqno;
    }

    public abstract byte[] ToBigEndianByteArray();

    public byte[] ToByteArray()
    {
      return this.ToBigEndianByteArray();
    }

    protected void EncodeTag(MemoryStream memStream, byte value)
    {
      memStream.WriteByte(value);
    }

    protected void EncodeUlong(MemoryStream memStream, ulong value)
    {
      if (null == memStream)
      {
        throw new ArgumentNullException("memStream");
      }

      var bytes = BitConverter.GetBytes(value);
      if (!BitConverter.IsLittleEndian)
      {
        // NB Ironfleet 2015 used big-endian marshalling
        // Verus 2023 uses little-endian marshalling
        Array.Reverse(bytes);
      }
      memStream.Write(bytes, 0, bytes.Length);
    }

    protected void EncodeBytes(MemoryStream memStream, byte[] value)
    {
      if (null == value)
      {
        throw new ArgumentNullException("value");
      }

      this.EncodeUlong(memStream, (ulong)value.Length);
      memStream.Write(value, 0, value.Length);
    }

    public static (bool, UInt64) ExtractLE64(byte[] byteArray, ref int offset)
    {
      if (byteArray.Length < offset + 8) {
        return (false, 0);
      }
      byte[] extractedBytes = byteArray.Skip(offset).Take(8).ToArray();
      if (!BitConverter.IsLittleEndian) {
        Array.Reverse(extractedBytes);
      }
      offset += 8;
      return (true, BitConverter.ToUInt64(extractedBytes, 0));
    }

    // public static UInt32 ExtractLE32(byte[] byteArray, int offset)
    // {
    //   byte[] extractedBytes = byteArray.Skip(offset).Take(4).ToArray();
    //   if (!BitConverter.IsLittleEndian) {
    //     Array.Reverse(extractedBytes);
    //   }
    //   return BitConverter.ToUInt32(extractedBytes, 0);
    // }

    static (bool, byte) DecodeTag(byte[] bytes, ref int offset)
    {
      if (bytes.Length < offset + 1) {
        return (false, 0);
      }
      byte result = bytes[offset];
      offset += 1;
      return (true, result);
    }
    static bool Skip(byte[] bytes, int skipLen, ref int offset)
    {
      if (bytes.Length < offset + skipLen) {
        return false;
      }
      offset += skipLen;
      return true;
    }

    // Returns null if bytes can't be parsed.
    public static MessageBase ParseMessageFromBytes(byte[] bytes)
    {
      bool ok;
      byte tag;
      int offset = 0;
      (ok, tag) = DecodeTag(bytes, ref offset);
      if (!ok) {
        Console.WriteLine("Short message at CSingleMessage tag");
        return null;
      }
      if (tag == 1) {
        // CSingleMessage.Ack
        // Ack.ack_seqno
        ulong ackSeqno;
        (ok, ackSeqno) = ExtractLE64(bytes, ref offset);
        if (!ok) {
          Console.WriteLine("Short message at CSingleMessage.Ack.ack_seqno");
          return null;
        }
        return new AckMessage(ackSeqno);
      } else if (tag == 0) {
        // CSingleMessage.Reply
        ulong seqno;
        (ok, seqno) = ExtractLE64(bytes, ref offset);
        if (!ok) {
          Console.WriteLine("Short message at CSingleMessage.seqno");
          return null;
        }
        ulong publicKeyLength;
        (ok, publicKeyLength) = ExtractLE64(bytes, ref offset);
        if (!ok) {
          Console.WriteLine("Short message at CSingleMessage.dst length");
          return null;
        }
        
        ok = Skip(bytes, (int) publicKeyLength, ref offset);
        if (!ok) {
          Console.WriteLine("Short message at CSingleMessage.dst body");
          return null;
        }

        (ok, tag) = DecodeTag(bytes, ref offset);
        if (!ok) {
          Console.WriteLine("Short message at CMessage tag");
          return null;
        }
        if (tag == 2) { // CMessage.Reply
          ulong replyKey;
          (ok, replyKey) = ExtractLE64(bytes, ref offset);
          return new ReplyMessage(seqno, replyKey);
        } else if (tag == 3) { // CMessage.Redirect
          ulong redirectKey;
          (ok, redirectKey) = ExtractLE64(bytes, ref offset);
          return new RedirectMessage(seqno, redirectKey);
        } else {
          Console.WriteLine("Unexpected CMessage tag {0}", tag);
          return null;
        }
      } else {
        Console.WriteLine("Unexpected CSingleMessage tag {0}", tag);
        return null;
      }
    }

    public void Send(IoScheduler<byte[]> scheduler, byte[] remote)
    {
      var a = this.ToBigEndianByteArray();
      remote = scheduler.HashPublicKey(remote);
      if (!scheduler.SendPacket(remote, a))
      {
        throw new InvalidOperationException("failed to send complete message.");
      }
    }

    public virtual void SendAck(IoScheduler<byte[]> scheduler, byte[] remote) {
      var ack_msg = new AckMessage(seqno);
      ack_msg.Send(scheduler, remote);
    }
  }

  public class ReplyMessage : MessageBase
  {
    public ulong replyKey;

    public ReplyMessage(ulong seqno, ulong replyKey) : base(seqno)
    {
      this.replyKey = replyKey;
    }

    public override string ToString()
    {
      return String.Format("ReplyMessage({0})", replyKey);
    }

    public override byte[] ToBigEndianByteArray()
    {
      Debug.Assert(false);  // no support for encoding this type.
      return new byte[0];
    }
  }

  public class RedirectMessage : MessageBase
  {
    public ulong redirectKey;

    public RedirectMessage(ulong seqno, ulong redirectKey) : base(seqno)
    {
      this.redirectKey = redirectKey;
    }

    public override string ToString()
    {
      return String.Format("RedirectMessage({0})", redirectKey);
    }

    public override byte[] ToBigEndianByteArray()
    {
      Debug.Assert(false);  // no support for encoding this type.
      return new byte[0];
    }
  }

  public class GetRequestMessage : MessageBase
  {
    public byte[] Value { get; set; }
    public byte[] myPublicKey;
    public ulong key;

    public GetRequestMessage(ulong seqno, byte[] myPublicKey, ulong key) : base(seqno)
    {
      this.myPublicKey = myPublicKey;
      this.key = key;    
    }

    public override byte[] ToBigEndianByteArray()
    {
      return this.Encode();
    }

    private byte[] Encode(bool retrans = false)
    {
      using (var memStream = new MemoryStream())
      {
        // CSingleMessage.Message tag
        this.EncodeTag(memStream, 0);
        // CSingleMessage.Message.seqno
        this.EncodeUlong(memStream, (ulong)seqno);
        // CSingleMessage.Message.dst
        this.EncodeBytes(memStream, myPublicKey);
        // CSingleMessage.Message.m
        // CMessage.GetRequest tag
        this.EncodeTag(memStream, 0);
        // CMessage.GetRequest.k
        this.EncodeUlong(memStream, key);
                
        return memStream.ToArray();
      }
    }
  }

  public class SetRequestMessage : MessageBase
  {
    public byte[] myPublicKey;
    public ulong key;
    public ulong sizeValue;
    public Random rnd;

    public SetRequestMessage(ulong seqno, byte[] myPublicKey, ulong key, ulong sizeValue) : base(seqno)
    {
      this.myPublicKey = myPublicKey;
      this.key = key;
      this.sizeValue = sizeValue;
      rnd = new Random();
    }

    public override byte[] ToBigEndianByteArray()
    {
      return this.Encode();
    }

    private byte[] Encode(bool retrans = false)
    {
      using (var memStream = new MemoryStream())
      {
        byte[] value = new byte[sizeValue];
                         
        rnd.NextBytes(value);

        // CSingleMessage.Message tag
        this.EncodeTag(memStream, 0);
        // CSingleMessage.Message.seqno
        this.EncodeUlong(memStream, (ulong)seqno);
        // CSingleMessage.Message.dst
        this.EncodeBytes(memStream, myPublicKey);
        // CSingleMessage.Message.m
        // CMessage.SetRequest tag
        this.EncodeTag(memStream, 1);
        // CMessage.GetRequest.k
        this.EncodeUlong(memStream, key);
        // CMessage.GetRequest.v
        // Option.Some tag
        this.EncodeTag(memStream, 1); // case for OptionalValue
        // Option.Some.v
        this.EncodeBytes(memStream, value);

        return memStream.ToArray();
      }
    }
  }

  public class ShardRequestMessage : MessageBase
  {
    public byte[] myPublicKey;
    public ulong k_lo, k_hi;
    public byte[] recipient;

    public ShardRequestMessage(ulong seqno, byte[] myPublicKey, ulong k_lo, ulong k_hi, byte[] recipient) : base(seqno)
    {
      this.myPublicKey = myPublicKey;
      this.k_lo = k_lo;
      this.k_hi = k_hi;
      this.recipient = recipient;
    }

    public override byte[] ToBigEndianByteArray()
    {
      return this.Encode();
    }

    private byte[] Encode(bool retrans = false)
    {
      using (var memStream = new MemoryStream())
      {
        // CSingleMessage.Message tag
        this.EncodeTag(memStream, 0);
        // CSingleMessage.Message.seqno
        this.EncodeUlong(memStream, (ulong)seqno);
        // CSingleMessage.Message.dst
        this.EncodeBytes(memStream, myPublicKey);
        // CSingleMessage.Message.m
        // CMessage.Shard tag
        this.EncodeTag(memStream, 4);

        // CMessage.Shard.kr
        // KeyRange.lo
        // Option.Some
        this.EncodeTag(memStream, 1);
        // Option.v
        this.EncodeUlong(memStream, (ulong)k_lo);

        // KeyRange.hi
        // Option.Some
        this.EncodeTag(memStream, 1);
        // Option.v
        this.EncodeUlong(memStream, (ulong)k_hi);

        // CMessage.Shard.recipient
        this.EncodeBytes(memStream, recipient);

        return memStream.ToArray();
      }
    }
  }

  public class AckMessage : MessageBase
  {
    public byte[] Value { get; set; }

    public AckMessage(ulong seqno) : base(seqno)
    {
    }

    public override byte[] ToBigEndianByteArray()
    {
      return this.Encode();
    }

    public override string ToString()
    {
      return String.Format("Ack({0})", seqno);
    }

    private byte[] Encode(bool retrans = false)
    {
      using (var memStream = new MemoryStream())
      {
        // CSingleMessage.Ack tag
        this.EncodeTag(memStream, 1);
        // CSingleMessage.Ack.ack_seqno
        this.EncodeUlong(memStream, (ulong)seqno);
        return memStream.ToArray();
      }
    }

    public override void SendAck(IoScheduler<byte[]> scheduler, byte[] remote) {
      // You know what we should not ack? Acks.
    }
  }

  public class ThreadParams
  {
    public Params ps;
    public ulong id;

    public ThreadParams(ulong i_id, Params i_ps)
    {
      id = i_id;
      ps = i_ps;
    }
  }

  public class Client
  {
    public int id;
    public Params ps;
    private ArrayBufferManager bufferManager;
    public IoScheduler<byte[]> scheduler;
    public ServiceIdentity serviceIdentity;

    private Client(int i_id, Params i_ps, ServiceIdentity i_serviceIdentity)
    {
      id = i_id;
      ps = i_ps;
      bufferManager = new ArrayBufferManager();
      serviceIdentity = i_serviceIdentity;
    }

    static public IEnumerable<Thread> StartSetupThreads(Params ps, ServiceIdentity serviceIdentity)
    {
      if (ps.NumThreads < 0)
      {
        throw new ArgumentException("count is less than 1", "count");
      }

      for (int i = 0; i < ps.NumSetupThreads; ++i)
      {
        var c = new Client(i, ps, serviceIdentity);
        Thread t = new Thread(c.Setup);
        t.Start();
        yield return t;
      }
    }

    static public IEnumerable<Thread> StartExperimentThreads(Params ps, ServiceIdentity serviceIdentity)
    {
      if (ps.NumThreads < 0)
      {
        throw new ArgumentException("count is less than 1", "count");
      }

      for (int i = 0; i < ps.NumThreads; ++i)
      {
        var c = new Client(i, ps, serviceIdentity);
        Thread t = new Thread(c.Experiment);
        t.Start();
        yield return t;
      }
    }

    public string ByteArrayToString(byte[] ba)
    {
      string hex = BitConverter.ToString(ba);
      return hex.Replace("-", "");
    }

    public void Setup()
    {
      scheduler = IoScheduler<byte[]>.CreateClient(serviceIdentity.Servers, bufferManager, ps.Verbose, serviceIdentity.UseSsl);
      byte[] myPublicKey = IronfleetCrypto.GetCertificatePublicKey(scheduler.MyCert);
            
      int serverIdx = 0;
      ulong seqNum = 0;
      ulong requestKey;

      for (requestKey = 0; requestKey < (ulong)ps.NumKeys; ++requestKey)
      {
        seqNum++;
        var reqMsg = new SetRequestMessage(seqNum, myPublicKey, requestKey, (ulong)ps.ValueSize);

        if (ps.Verbose || (seqNum%100)==0) {
          Console.WriteLine("Sending set request message with seq {0}, key {1} to server {2}", seqNum, requestKey, serverIdx);
        }
        reqMsg.Send(scheduler, serviceIdentity.Servers[serverIdx].PublicKey);
                
        // Wait for the reply
        var receivedReply = false;
        while (!receivedReply)
        {
          byte[] bytes = Receive();
          var endTime = HiResTimer.Ticks;
          if (bytes == null) {
            //serverIdx = (serverIdx + 1) % serviceIdentity.Servers.Count();
            Console.WriteLine("#timeout; retrying {0}", serverIdx);
            reqMsg.Send(scheduler, serviceIdentity.Servers[serverIdx].PublicKey);
            continue;
          }

          MessageBase recvMsg = MessageBase.ParseMessageFromBytes(bytes);
          if (recvMsg != null) {
            if (ps.Verbose) {
              Console.WriteLine("Recieved message: {0}", recvMsg);
            }
            recvMsg.SendAck(scheduler, serviceIdentity.Servers[serverIdx].PublicKey); // Acknowledge non-ack messages
          }

          ReplyMessage reply = recvMsg as ReplyMessage;
          if (reply != null) {
            if (reply.replyKey == requestKey) {
              receivedReply = true;
            }
          }
        }
      }
    }

    private void ReceiveReply(int serverIdx, byte[] myPublicKey, ulong requestKey, bool receiveOnlyAcks,
                              bool expectRedirect = false)
    {
      while (true)
      {
        byte[] bytes = Receive();
        if (bytes == null) {
          Console.WriteLine("#timeout; retrying {0}", serverIdx);
          // Kinda weird that Ironfleet didn't actually re-send here (as in the Setup case).
          continue;
        }

        MessageBase recvMsg = MessageBase.ParseMessageFromBytes(bytes);
        if (recvMsg == null) {
          continue;
        }

        if (ps.Verbose) {
          Console.WriteLine("Received message: {0}", recvMsg);
        }

        recvMsg.SendAck(scheduler, serviceIdentity.Servers[serverIdx].PublicKey); // Acknowledge non-ack messages

        switch (recvMsg) {
          case AckMessage ack:
            if (receiveOnlyAcks) { return; }
            break;
          case ReplyMessage reply:
            if (reply.replyKey == requestKey && !expectRedirect) { return; }
            break;
          case RedirectMessage redirect:
            if (redirect.redirectKey == requestKey && expectRedirect) { return; }
            break;
          default:
            Console.WriteLine("Received unexpected message type");
            break;
        }
      }
    }

    public void Experiment()
    {
      ulong requestKey;
      int serverIdx = 0;
            
      scheduler = IoScheduler<byte[]>.CreateClient(serviceIdentity.Servers, bufferManager, ps.Verbose, serviceIdentity.UseSsl);

      byte[] myPublicKey = IronfleetCrypto.GetCertificatePublicKey(scheduler.MyCert);
      ulong seqNum = 0;
            
      // Test the functionality of the Sharding
      if (ps.Workload == 'f')
      {
        // A delegation can delegate at most 61 keys, so make sure
        // there can't be that many keys in the range by having the
        // range be smaller than 61.
        ulong k_lo = 125;
        ulong k_hi = 175;
        requestKey = 150;
        var recipient = serviceIdentity.Servers[(serverIdx + 1) % serviceIdentity.Servers.Count()];

        seqNum++;
        var msg = new GetRequestMessage(seqNum, myPublicKey, requestKey);
        msg.Send(scheduler, serviceIdentity.Servers[serverIdx].PublicKey);
        ReceiveReply(serverIdx, myPublicKey, requestKey, false);

        seqNum++;
        Console.WriteLine("Sending a Shard request with a sequence number {0}", seqNum);
        var shardMessage = new ShardRequestMessage(seqNum, myPublicKey, k_lo, k_hi, recipient.PublicKey);
        shardMessage.Send(scheduler, serviceIdentity.Servers[serverIdx].PublicKey);
        ReceiveReply(serverIdx, myPublicKey, requestKey, true);

        Thread.Sleep(5000);

        Console.WriteLine("Sending a GetRequest after a Shard, expect a redirect");

        seqNum++;
        msg = new GetRequestMessage(seqNum, myPublicKey, requestKey);
        msg.Send(scheduler, serviceIdentity.Servers[(serverIdx + 0) % serviceIdentity.Servers.Count()].PublicKey);
        ReceiveReply(serverIdx, myPublicKey, requestKey, false, expectRedirect: true);

        Thread.Sleep(5000);

        Console.WriteLine("Sending a GetRequest after a Shard to the second host, expect a reply");
        // Must use sequence number 1 since this is the first message
        // to this server.
        msg = new GetRequestMessage(1, myPublicKey, requestKey);
        msg.Send(scheduler, serviceIdentity.Servers[(serverIdx + 1) % serviceIdentity.Servers.Count()].PublicKey);
        ReceiveReply((serverIdx + 1) % serviceIdentity.Servers.Count(), myPublicKey, requestKey, false);

        Console.WriteLine("Successfully received reply");
                
        return;
      }

      // Run an actual workload
      while (true)
      {
        seqNum++;
        var receivedReply = false;
        requestKey = seqNum % (ulong)ps.NumKeys;
                               
        MessageBase msg;
        if (ps.Workload == 'g') 
        {
          msg = new GetRequestMessage(seqNum, myPublicKey, requestKey);
        }
        else
        {
          msg = new SetRequestMessage(seqNum, myPublicKey, requestKey, (ulong)ps.ValueSize);
        }

        var startTime = HiResTimer.Ticks;
        msg.Send(scheduler, serviceIdentity.Servers[serverIdx].PublicKey);
        
        // Wait for the reply
                
        while (!receivedReply)
        {
          byte[] bytes = Receive();
          if (bytes == null) {
            Console.WriteLine("#timeout; retrying {0}", serverIdx);
            msg.Send(scheduler, serviceIdentity.Servers[serverIdx].PublicKey);
            continue;
          }
          var endTime = HiResTimer.Ticks;

          MessageBase recvMsg = MessageBase.ParseMessageFromBytes(bytes);
          if (recvMsg == null) {
            continue;
          }

          if (ps.Verbose) {
            Console.WriteLine("Received message: {0}", recvMsg);
          }

          if (seqNum % 100 == 0) {
            // Ack in 100-message batches.
            recvMsg.SendAck(scheduler, serviceIdentity.Servers[serverIdx].PublicKey); // Acknowledge non-ack messages
          }

          switch (recvMsg) {
            case AckMessage ack:
              // ignore
              break;
            case ReplyMessage reply:
              if (reply.replyKey == requestKey) {
                receivedReply = true;
                Console.WriteLine("#req {0} {1} {2}",
                                  id,
                                  seqNum,
                                  HiResTimer.TicksToMilliseconds(endTime - startTime));
              }
              break;
            default:
              Console.WriteLine("Received unexpected message type");
              break;
          }
        }
      }
    }

    private byte[] Receive()
    {
      bool ok;
      bool timedOut;
      byte[] remote;
      Option<byte[]> buffer;
      scheduler.ReceivePacket(1000, out ok, out timedOut, out remote, out buffer);
      if (ok && !timedOut && buffer is Some<byte[]> some) {
        return some.Value;
      }
      else {
        return null;
      }
    }
  }
}
