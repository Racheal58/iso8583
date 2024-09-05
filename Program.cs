// See https://aka.ms/new-console-template for more information
using CSI.IsoFunctions;

namespace CSI
{
  class Program {
    static async Task Main(string[] args) {
      await IsoFunction.GenerateEchoMessage();
    //await IsoFunction.GenerateKeyExchange();
      //await IsoFunction.PushJournalRequest();
      IsoFunction.GetClearZPK("", "");
    }
  }
}
