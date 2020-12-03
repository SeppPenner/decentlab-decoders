
/* https://decentlab.squarespace.com/products/laser-distance-level-sensor-for-lorawan */

using System;
using System.IO;
using System.Collections.Generic;

public class DecentlabDecoder
{
  private delegate double conversion(double[] x);
  public const int PROTOCOL_VERSION = 2;

  private class Sensor
  {
    internal int length { get; set; }
    internal List<SensorValue> values { get; set; }
    internal Sensor(int length, List<SensorValue> values)
    {
      this.length = length;
      this.values = values;
    }
  }

  private class SensorValue
  {
    internal string name { get; set; }
    internal string unit { get; set; }
    internal conversion convert;
    internal SensorValue(string name, string unit, conversion convert)
    {
      this.name = name;
      this.unit = unit;
      this.convert = convert;
    }
  }

  private static readonly List<Sensor> SENSORS = new List<Sensor>() {
    new Sensor(11, new List<SensorValue>() {
      new SensorValue("Distance: average", "mm", x => x[0]),
      new SensorValue("Distance: minimum", "mm", x => x[1]),
      new SensorValue("Distance: maximum", "mm", x => x[2]),
      new SensorValue("Distance: median", "mm", x => x[3]),
      new SensorValue("Distance: 10th percentile", "mm", x => x[4]),
      new SensorValue("Distance: 25th percentile", "mm", x => x[5]),
      new SensorValue("Distance: 75th percentile", "mm", x => x[6]),
      new SensorValue("Distance: 90th percentile", "mm", x => x[7]),
      new SensorValue("Distance: most frequent value", "mm", x => x[8]),
      new SensorValue("Number of samples", null, x => x[9]),
      new SensorValue("Total acquisition time", "ms", x => x[10] / 1.024)
    }),
    new Sensor(1, new List<SensorValue>() {
      new SensorValue("Battery voltage", "V", x => x[0] / 1000)
    })
  };

  private static int ReadInt(Stream stream)
  {
    return (stream.ReadByte() << 8) + stream.ReadByte();
  }


  public static Dictionary<string, Tuple<double, string>> Decode(byte[] msg)
  {
    return Decode(new MemoryStream(msg));
  }

  public static Dictionary<string, Tuple<double, string>> Decode(String msg)
  {
    byte[] output = new byte[msg.Length / 2];
    for (int i = 0, j = 0; i < msg.Length; i += 2, j++)
    {
      output[j] = (byte)int.Parse(msg.Substring(i, 2), System.Globalization.NumberStyles.HexNumber);
    }
    return Decode(output);
  }

  public static Dictionary<string, Tuple<double, string>> Decode(Stream msg)
  {
    var version = msg.ReadByte();
    if (version != PROTOCOL_VERSION)
    {
      throw new InvalidDataException("protocol version " + version + " doesn't match v2");
    }

    var result = new Dictionary<string, Tuple<double, string>>();
    result["Protocol version"] = new Tuple<double, string>(version, null);

    var deviceId = ReadInt(msg);
    result["Device ID"] = new Tuple<double, string>(deviceId, null);

    var flags = ReadInt(msg);
    foreach (var sensor in SENSORS)
    {
      if ((flags & 1) == 1)
      {
        double[] x = new double[sensor.length];
        for (int i = 0; i < sensor.length; i++)
        {
          x[i] = ReadInt(msg);
        }
        foreach (var val in sensor.values)
        {
          if (val.convert != null)
          {
            result[val.name] = new Tuple<double, string>(val.convert(x), val.unit);
          }
        }
      }
      flags >>= 1;
    }

    return result;
  }
}
public class Program
{
  public static void Main()
  {
    var payloads = new string[] {
      "0211c90003119b117611bc119e118a119411a811a81194006401990abd",
      "0211c900020abd"
    };

    foreach (var pl in payloads)
    {
      var decoded = DecentlabDecoder.Decode(pl);
      foreach (var k in decoded.Keys)
      {
        Console.WriteLine(k + ": " + decoded[k]);
      }
      Console.WriteLine();
    }
  }
}
