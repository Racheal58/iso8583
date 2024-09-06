using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace CSI.Utils;

public class Util {
  public static string GeneratePinBlock(string pin, string zpk, string pan) {
    byte[] pinBlock = StringToByteArray($"0{pin.Length}{pin}".PadRight(16, 'F'));

    byte[] panBlock = StringToByteArray(pan.Substring(pan.Length - 13, 12).PadRight(16, '0'));

    byte[] clearPinBlock = XorIt(pinBlock, panBlock);

    byte[] encryptedPinBlock = Encrypt(clearPinBlock, StringToByteArray(zpk));

    return ByteArrayToString(encryptedPinBlock);
  }


  public static string GetClearZpk(string field53KeResponse, string encryptedZmk) {
    string encryptedZpk = field53KeResponse[..32];

    string kcv = field53KeResponse.Substring(32, 6);

    string encryptedZpkPartA = encryptedZpk[..16];
    string encryptedZpkPartB = encryptedZpk.Substring(16, 16);

    string encryptedZmkPartA = encryptedZmk[..16];
    byte[] encryptedZmkPartB = StringToByteArray(encryptedZmk.Substring(16, 16));

    string zmkPartBVariant1 = ByteArrayToString(XorIt(encryptedZmkPartB, StringToByteArray("A6".PadRight(16, '0'))));
    string zmkPartBVariant2 = ByteArrayToString(XorIt(encryptedZmkPartB, StringToByteArray("5A".PadRight(16, '0'))));

    byte[] result1 = Decrypt(StringToByteArray(encryptedZpkPartA.PadRight(32, '0')),
      StringToByteArray(encryptedZmkPartA + zmkPartBVariant1));

    byte[] result2 = Decrypt(StringToByteArray(encryptedZpkPartB.PadRight(32, '0')),
      StringToByteArray(encryptedZmkPartA + zmkPartBVariant2));


    string clearzpk = ByteArrayToString(result1)[..16] + ByteArrayToString(result2)[..16];

    Console.WriteLine($"\nKCV validation is: {kcv == GetKvc(StringToByteArray(clearzpk))}");

    return clearzpk;
  }

  static string GetKvc(byte[] key) {
    byte[] result = new byte[8];

    DesEdeEngine desEngine = new();
    desEngine.Init(true, new DesEdeParameters(key));
    byte[] kcvBytes = new byte[8];
    desEngine.ProcessBlock(result, 0, kcvBytes, 0);

    return ByteArrayToString(kcvBytes)[..6];
  }

  static byte[] Decrypt(byte[] data, byte[] key) {
    byte[] result = new byte[data.Length / 2];

    DesEdeParameters keyParams = new(key);
    DesEdeEngine desEngine = new();
    desEngine.Init(false, keyParams);
    desEngine.ProcessBlock(data, 0, result, 0);

    return result;
  }
  static byte[] Encrypt(byte[] data, byte[] key) {
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
}