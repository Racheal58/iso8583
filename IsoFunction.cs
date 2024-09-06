using System.Text;
using System.Text.Json;
using CSI.Utils;
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
      RemotePort = "",
      RemoteInterface = "",
      Name = "Racheal"
    };

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
    var msg = new Iso8583Message(0800);
    msg.Fields.Add(7, "0905091101");
    msg.Fields.Add(11, "642795");
    msg.Fields.Add(32, "040");
    msg.Fields.Add(70, "301");

    await Connect();

    SendRequestHandlerCtrl sndCtrl = _client.SendExpectingResponse(msg, 3000);
    sndCtrl.WaitCompletion();
    sndCtrl.Request.WaitResponse();

    Message response = (Message)sndCtrl.Request.ReceivedMessage;
    Console.Write($"Echo Message: {response}");
    return string.Empty;
  }

  public static async Task<string> GenerateKeyExchangeMessage() {
    var msg = new Iso8583Message(0800);
    msg.Fields.Add(3, "301000");
    msg.Fields.Add(7, DateTime.Now.ToString("MMddHHmmss"));
    msg.Fields.Add(11, "123456");
    msg.Fields.Add(32, "040");
    msg.Fields.Add(37, "123456789ABC");
    msg.Fields.Add(70, "101");

    await Connect();

    SendRequestHandlerCtrl sndCtrl = _client.SendExpectingResponse(msg, 10000); 
    sndCtrl.WaitCompletion();
    sndCtrl.Request.WaitResponse();

    Message response = (Message)sndCtrl.Request.ReceivedMessage;
    Console.WriteLine($"Key Exchange Message: {response}");

    string key = (string)response[53].Value;
    Console.WriteLine($"Key exchange response field 53: {key}");

    return key;
  }

  public static async Task<string> GenerateFinancialMessage(string key) {
    var msg = new Iso8583Message(0200);

    string pan = "5559405048128222";
    string amount = "000000010000";
    string pin = "1234";

    // Convert key to clear ZPK (Zero Padding Key)
    string clearZpk = Util.GetClearZpk(key, "63E4880A2D502DD8E835C68DD8061BBB");

    // Generate the PIN Block using the clear ZPK and card PAN
    string pinBlock = Util.GeneratePinBlock(pin, clearZpk, pan);

    // Set the fields for the financial message
    msg.Fields.Add(2, pan); // Primary Account Number
    msg.Fields.Add(4, amount); // Transaction Amount
    msg.Fields.Add(7, "0905091101"); // Transmission Date and Time
    msg.Fields.Add(11, "642795"); // System Trace Audit Number
    msg.Fields.Add(32, "4008"); // Acquiring Institution ID
    msg.Fields.Add(37, "451298"); // Retrieval Reference Number
    msg.Fields.Add(41, "20351254"); // Terminal ID
    msg.Fields.Add(49, "566"); // Currency Code (Nigerian Naira)
    // msg.Fields.Add(52, pinBlock); // PIN Block

    Console.WriteLine($"Generated financial message: {msg}");

    await Connect();

    SendRequestHandlerCtrl sndCtrl = _client.SendExpectingResponse(msg, 3000); 
    sndCtrl.WaitCompletion();
    sndCtrl.Request.WaitResponse();

    Message response = (Message)sndCtrl.Request.ReceivedMessage;
    Console.WriteLine($"Financial Message: {response}");

    // string keyIn = (string)response[53].Value;
    // Console.WriteLine($"Key exchange response field 53: {keyIn}");

    return string.Empty;
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