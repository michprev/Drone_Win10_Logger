using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;

namespace Drone_Win10_Logger
{
    class Data
    {
        public int Roll { get; set; }
        public int Pitch { get; set; }
        public int Yaw { get; set; }
        public int Throttle { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            SerialDevice serial;

            Queue<Data> queue = new Queue<Data>();

            if (File.Exists("log.txt"))
                File.Delete("log.txt");

            Task.Run(() =>
            {
                StringBuilder sb = new StringBuilder();
                sb.Capacity = 1000001;

                int c = 0;

                while (true)
                {
                    while (queue.Count > 0 && sb.Length < 900000)
                    {
                        Data d = queue.Dequeue();

                        sb.AppendFormat("{0};{1};{2};{3};{4}\r\n", d.Roll / 65536.0, d.Pitch / 65536.0, d.Yaw / 65536.0, d.Throttle, c * 5);
                        c++;
                    }

                    File.AppendAllText("log.txt", sb.ToString());
                    sb.Clear();
                }
            });

            Task.Run(async () =>
            {
                Console.WriteLine("Connected to COM4");
                var selector = SerialDevice.GetDeviceSelector("COM4");
                var devices = await DeviceInformation.FindAllAsync(selector);

                if (devices.Count > 0)
                {
                    var id = devices.First().Id;
                    serial = await SerialDevice.FromIdAsync(id);
                    serial.BaudRate = 115200;
                    serial.StopBits = SerialStopBitCount.One;
                    serial.DataBits = 8;
                    serial.Parity = SerialParity.None;
                    serial.Handshake = SerialHandshake.None;

                    DataReader dr = new DataReader(serial.InputStream);
                    dr.ByteOrder = ByteOrder.BigEndian;

                    int roll, pitch, yaw, throttle;

                    while (true)
                    {
                        await dr.LoadAsync(16);

                        roll = dr.ReadInt32();
                        pitch = dr.ReadInt32();
                        yaw = dr.ReadInt32();
                        throttle = dr.ReadInt32();

                        queue.Enqueue(new Data() { Roll = roll, Pitch = pitch, Yaw = yaw, Throttle = throttle });
                    }
                }
            });

            while (true) ;
        }
    }
}
