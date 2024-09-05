using System.Text;
using System.Text.Json;
using BIM_ISO8583.NET;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Trx.Communication.Channels;
using Trx.Communication.Channels.Sinks;
using Trx.Communication.Channels.Sinks.Framing;
using Trx.Communication.Channels.Tcp;
using Trx.Coordination.TupleSpace;
using Trx.Messaging;
using Trx.Messaging.Iso8583;

namespace CSI.IsoFunctions;

public class IsoFunction {

  static readonly TcpClientChannel _client = new TcpClientChannel(LocalPipeline(),
  new TupleSpace<ReceiveDescriptor>(), new FieldsMessagesIdentifier(new[] {11})) {
      RemotePort = 12000,
      RemoteInterface = "52.234.156.59",
      Name = "Merchant"
    };

    // public IsoFunction(string hostname, string terminalCode) {
    //   var pipeline = new Pipeline();
    //   pipeline.Push(new ReconnectionSink());
    //   pipeline.Push(new NboFrameLengthSink(2) {IncludeHeaderLength = false, MaxFrameLength = 1024});
    //   pipeline.Push(new MessageFormatterSink(new Iso8583MessageFormatter((@"./Formatters/Iso8583Ascii1987.xml"))));
    //     var ts = new TupleSpace<ReceiveDescriptor>();

    //   _client = new TcpClientChannel(pipeline, ts, new FieldsMessagesIdentifier(new[] {11})) {
    //     RemotePort = 12000,
    //     RemoteInterface = hostname,
    //     Name = "Merchant"
    //   };

    //   _client.Connect();



    //   _terminalCode = terminalCode;

    //   _sequencer = new VolatileStanSequencer();

    //   // return string.Empty;
    // }

  public static Pipeline LocalPipeline() {
    var pipeline = new Pipeline();
    pipeline.Push(new ReconnectionSink());
    pipeline.Push(new NboFrameLengthSink(2) {IncludeHeaderLength = false, MaxFrameLength = 1024});
    pipeline.Push(new MessageFormatterSink(new Iso8583MessageFormatter((@"./Formatters/Iso8583Ascii1987.xml"))));
      var ts = new TupleSpace<ReceiveDescriptor>();

    return pipeline;
  }

  public static async Task Connect() {
    _client.Connect();

    await Task.Delay(5000); 
  }
  public static async Task<string> GenerateEchoMessage() {
    int field7 = 7;
    int field11 = 11;
    int field70 = 70;

    var echoMsg = new Iso8583Message(0800);
    echoMsg.Fields.Add(field7, "0905091101");
    echoMsg.Fields.Add(field11, "642795");
    echoMsg.Fields.Add(32, "040");
    echoMsg.Fields.Add(field70, "101");

    await Connect();

    SendRequestHandlerCtrl sndCtrl = _client.SendExpectingResponse(echoMsg, 3000);
    sndCtrl.WaitCompletion();
    sndCtrl.Request.WaitResponse();

    Message response = (Message)sndCtrl.Request.ReceivedMessage;
    Console.Write($"echoMsg {response}");
    return string.Empty;
  }

  public static async Task<string> GenerateKeyExchange() {
    int field3 = 3;
    int field7 = 7;
    int field11 = 11;
    int field32 = 32;
    int field70 = 70;

    var echoMsg = new Iso8583Message(0800);
    //echoMsg.Fields.Add(field3, "301000");
    //echoMsg.Fields.Add(field7, DateTime.Now.ToString("MMddHHmmss"));
    echoMsg.Fields.Add(field11, "123457");
    echoMsg.Fields.Add(field32, "046");
    echoMsg.Fields.Add(field70, "101");

    await Connect();

    SendRequestHandlerCtrl sndCtrl = _client.SendExpectingResponse(echoMsg, 1000);
    sndCtrl.WaitCompletion();
    sndCtrl.Request.WaitResponse();

    Message response = (Message)sndCtrl.Request.ReceivedMessage;
    Console.WriteLine($"Keyexchange: {response}");
    return string.Empty;
  }

  public static string GeneratePinBlock(string pin, string zpk, string pan)
    {
        byte[] pinBlock = StringToByteArray($"0{pin.Length}{pin}".PadRight(16, 'F'));

        byte[] panBlock = StringToByteArray(pan.Substring(pan.Length - 13, 12).PadRight(16, '0'));

        byte[] clearPinBlock = XorIt(pinBlock, panBlock);

        byte[] encryptedPinBlock = Encrypt(clearPinBlock, StringToByteArray(zpk));

        return ByteArrayToString(encryptedPinBlock);
    }


    public static string GetClearZPK(string field53Response, string encryptedZmk) {
    string encryptedZpk = field53Response.Substring(0, 32);

    Console.WriteLine(field53Response);

    string kcv = field53Response.Substring(32, 6);

    string encryptedZpkPartA = encryptedZpk.Substring(0, 16);
    string encryptedZpkPartB = encryptedZpk.Substring(16, 16);

    string encryptedZmkPartA = encryptedZmk.Substring(0, 16);
    string encryptedZmkPartB = encryptedZmk.Substring(16, 16);

    string zmkPartBVariant = ApplyVariant(encryptedZmkPartB, "A6");
    string zmkPartBVariant2 = ApplyVariant(encryptedZmkPartB, "5A");

    string result1 = Decrypt(StringToByteArray(encryptedZpkPartA.PadRight(32, '0')), StringToByteArray(encryptedZmkPartA + zmkPartBVariant)).Substring(0, 16);
    string result2 = Decrypt(StringToByteArray(encryptedZpkPartB.PadRight(32, '0')), StringToByteArray(encryptedZmkPartA + zmkPartBVariant2)).Substring(0, 16);

    Console.WriteLine(result1);
    Console.WriteLine(result2);

    string clearzpk = result1 + result2;

    Console.WriteLine(kcv == GetKVC(StringToByteArray(clearzpk)));

    return string.Empty;
  }

    static string GetKVC(byte[] key) {
    byte[] result = new byte[8];

    DesEdeEngine desEngine = new();
    desEngine.Init(true, new KeyParameter(key));
    byte[] kcvBytes = new byte[8];
    desEngine.ProcessBlock(result, 0, kcvBytes, 0);

    string a = ByteArrayToString(kcvBytes);

    Console.WriteLine(a);

    return ByteArrayToString(kcvBytes).Substring(0, 6);
  }

  static string Decrypt(byte[] data, byte[] key) {
    byte[] result = new byte[8];

    DesEdeParameters keyParams = new(key);
    DesEdeEngine desEngine = new();
    desEngine.Init(false, keyParams);
    desEngine.ProcessBlock(data, 0, result, 0);

    return ByteArrayToString(result);
  }

  static byte[] Encrypt(byte[] data, byte[] key)
    {
        byte[] result = new byte[8];

        DesEdeParameters keyParams = new(key);
        DesEdeEngine desEngine = new();
        desEngine.Init(true, keyParams);
        desEngine.ProcessBlock(data, 0, result, 0);

        return result;
    }

  static string ApplyVariant(string data, string variant) {
    byte[] dataArr = StringToByteArray(data);
    byte[] variantArr = StringToByteArray(variant.PadRight(16, '0'));

    return ByteArrayToString(XorIt(dataArr, variantArr));
  }

    static byte[] StringToByteArray(string hex) {
      int numberChars = hex.Length;

      byte[] bytes = new byte[numberChars / 2];

      for (int i = 0; i < numberChars; i += 2)
        bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);

      return bytes;
    }

    static string ByteArrayToString(byte[] bytes) {
      return BitConverter.ToString(bytes).Replace("-", "");
    }

    static byte[] XorIt(byte[] key, byte[] input) {
      byte[] bytes = new byte[input.Length];
        for (int i = 0; i < input.Length; i++)
          bytes[i] = (byte)(input[i] ^ key[i % key.Length]);

      return bytes;
    }


  public static async Task PushJournalRequest() {

      HttpClient client = new HttpClient();

      client.DefaultRequestHeaders.Add("x-api-key", "");
      var req = new PushJournal();
      var res = await client.PostAsync("http://52.234.156.59:31000/pushjournal/api/push-journal/",
      new StringContent(JsonSerializer.Serialize(new PushJournal()), Encoding.UTF8, "application/json"));

      Console.WriteLine(await res.Content.ReadAsStringAsync());
  }
}