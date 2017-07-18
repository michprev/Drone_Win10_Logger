using System;
using System.Collections.Concurrent;
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
        public double GyroX { get; set; }
        public double GyroY { get; set; }
        public double GyroZ { get; set; }
        public double AccelX { get; set; }
        public double AccelY { get; set; }
        public double AccelZ { get; set; }
        public double Temperature { get; set; }
        public double Roll { get; set; }
        public double Pitch { get; set; }
        public double Yaw { get; set; }
        public int Throttle { get; set; }
        public int DeltaT { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            SerialDevice serial;
            ConcurrentQueue<Data> queue = new ConcurrentQueue<Data>();

            DateTime now = DateTime.Now;
            string fileName = String.Format("log-{0}-{1}-{2}_{3}-{4}-{5}.csv", now.Day, now.Month, now.Year, now.Hour, now.Minute, now.Second);
            bool[] logEnabled = new bool[11];


            Task.Run(() =>
            {
                StringBuilder sb = new StringBuilder();
                sb.Capacity = 1000001;

                int c = 0;
                double time = 0;

                while (true)
                {
                    while (queue.Count > 0 && sb.Length < 900000)
                    {
                        Data d;
                        if (queue.TryDequeue(out d))
                        {
                            if (logEnabled[0])
                            {
                                sb.Append(d.AccelX).Append(';');
                            }
                            if (logEnabled[1])
                            {
                                sb.Append(d.AccelY).Append(';');
                            }
                            if (logEnabled[2])
                            {
                                sb.Append(d.AccelZ).Append(';');
                            }
                            if (logEnabled[3])
                            {
                                sb.Append(d.GyroX).Append(';');
                            }
                            if (logEnabled[4])
                            {
                                sb.Append(d.GyroY).Append(';');
                            }
                            if (logEnabled[5])
                            {
                                sb.Append(d.GyroZ).Append(';');
                            }
                            if (logEnabled[6])
                            {
                                sb.Append(d.Temperature).Append(';');
                            }
                            if (logEnabled[7])
                            {
                                sb.Append(d.Roll).Append(';');
                            }
                            if (logEnabled[8])
                            {
                                sb.Append(d.Pitch).Append(';');
                            }
                            if (logEnabled[9])
                            {
                                sb.Append(d.Yaw).Append(';');
                            }
                            if (logEnabled[10])
                            {
                                sb.Append(d.Throttle).Append(';');
                            }

                            time += d.DeltaT / 1000.0;

                            sb.Append(time).Append("\r\n");

                            c++;
                        }
                    }

                    if (sb.Length > 0)
                    {
                        File.AppendAllText(fileName, sb.ToString().Replace(',', '.'));
                        sb.Clear();
                    }
                }
            });

            Task.Run(async () =>
            {
                string selector = SerialDevice.GetDeviceSelectorFromUsbVidPid(0x0483, 0x3748);
                var devices = await DeviceInformation.FindAllAsync(selector);

                if (devices.Count == 0)
                {
                    selector = SerialDevice.GetDeviceSelectorFromUsbVidPid(0x0483, 0x374B);
                    devices = await DeviceInformation.FindAllAsync(selector);
                }

                if (devices.Count > 0)
                {
                    var id = devices.First().Id;
                    serial = await SerialDevice.FromIdAsync(id);
                    serial.BaudRate = 2000000;
                    serial.StopBits = SerialStopBitCount.One;
                    serial.DataBits = 8;
                    serial.Parity = SerialParity.None;
                    serial.Handshake = SerialHandshake.None;

                    Console.WriteLine(String.Format("Connected to {0}: {1}", serial.PortName, devices.First().Name));

                    DataReader dr = new DataReader(serial.InputStream);
                    dr.ByteOrder = ByteOrder.BigEndian;

                    do
                    {
                        await dr.LoadAsync(1);
                    } while (dr.ReadByte() != 0x33);

                    // load which data will be sent
                    await dr.LoadAsync(2);
                    UInt16 logOptions =  dr.ReadUInt16();
                    uint logLength = 0;

                    Console.WriteLine("Options read {0}", logOptions);

                    int pos = 0;

                    for (int i = 0x8000; i > 0; i /= 2)
                    {
                        if ((logOptions & i) != 0)
                        {
                            logEnabled[pos] = true;
                        }
                        else
                        {
                            logEnabled[pos] = false;
                        }

                        pos++;
                    }

                    StringBuilder headerBuilder = new StringBuilder();

                    if (logEnabled[0]) {
                        headerBuilder.Append("Accel_X;");
                        logLength += 2;
                    }
                    if (logEnabled[1]) {
                        headerBuilder.Append("Accel_Y;");
                        logLength += 2;
                    }
                    if (logEnabled[2]) {
                        headerBuilder.Append("Accel_Z;");
                        logLength += 2;
                    }
                    if (logEnabled[3]) {
                        headerBuilder.Append("Gyro_X;");
                        logLength += 2;
                    }
                    if (logEnabled[4]) {
                        headerBuilder.Append("Gyro_Y;");
                        logLength += 2;
                    }
                    if (logEnabled[5]) {
                        headerBuilder.Append("Gyro_Z;");
                        logLength += 2;
                    }
                    if (logEnabled[6]) {
                        headerBuilder.Append("Temperature;");
                        logLength += 2;
                    }
                    if (logEnabled[7]) {
                        headerBuilder.Append("Roll;");
                        logLength += 4;
                    }
                    if (logEnabled[8]) {
                        headerBuilder.Append("Pitch;");
                        logLength += 4;
                    }
                    if (logEnabled[9]) {
                        headerBuilder.Append("Yaw;");
                        logLength += 4;
                    }
                    if (logEnabled[10]) {
                        headerBuilder.Append("Throttle;");
                        logLength += 2;
                    }

                    // time delta
                    logLength += 2;

                    headerBuilder.Append("Time\r\n");

                    if (File.Exists(fileName))
                        File.Delete(fileName);
                    File.AppendAllText(fileName, headerBuilder.ToString());


                    double gyroX, gyroY, gyroZ, accelX, accelY, accelZ;
                    gyroX = gyroY = gyroZ = accelX = accelY = accelZ = 0;
                    double temperature = 0;
                    double roll, pitch, yaw;
                    roll = pitch = yaw = 0;
                    int throttle = 0;
                    int deltaT = 0;
                    double gDiv = Math.Pow(2, 15) / 2000; // +- 2000 deg/s FSR
                    double aDiv = Math.Pow(2, 15) / 16; // +- 16 g FSR

                    while (true)
                    {
                        do
                        {
                            await dr.LoadAsync(1);
                        } while (dr.ReadByte() != 0x33);

                        await dr.LoadAsync(logLength + 1);

                        byte[] tmp = new byte[logLength + 1];

                        dr.ReadBytes(tmp);

                        byte crc = tmp[logLength];

                        byte localCrc = 0x00;
                        for (int i = 0; i < logLength; i++)
                            localCrc ^= tmp[i];

                        if (crc == localCrc)
                        {
                            int parsePos = 0;

                            if (logEnabled[0]) {
                                byte swap = tmp[parsePos + 1];
                                tmp[parsePos + 1] = tmp[parsePos];
                                tmp[parsePos] = swap;
                                accelX = BitConverter.ToInt16(tmp, parsePos);
                                accelX /= aDiv;

                                parsePos += 2;
                            }
                            if (logEnabled[1]) {
                                byte swap = tmp[parsePos + 1];
                                tmp[parsePos + 1] = tmp[parsePos];
                                tmp[parsePos] = swap;
                                accelY = BitConverter.ToInt16(tmp, parsePos);
                                accelY /= aDiv;

                                parsePos += 2;
                            }
                            if (logEnabled[2]) {
                                byte swap = tmp[parsePos + 1];
                                tmp[parsePos + 1] = tmp[parsePos];
                                tmp[parsePos] = swap;
                                accelZ = BitConverter.ToInt16(tmp, parsePos);
                                accelZ /= aDiv;

                                parsePos += 2;
                            }
                            if (logEnabled[3]) {
                                byte swap = tmp[parsePos + 1];
                                tmp[parsePos + 1] = tmp[parsePos];
                                tmp[parsePos] = swap;
                                gyroX = BitConverter.ToInt16(tmp, parsePos);
                                gyroX /= gDiv;

                                parsePos += 2;
                            }
                            if (logEnabled[4]) {
                                byte swap = tmp[parsePos + 1];
                                tmp[parsePos + 1] = tmp[parsePos];
                                tmp[parsePos] = swap;
                                gyroY = BitConverter.ToInt16(tmp, parsePos);
                                gyroY /= gDiv;

                                parsePos += 2;
                            }
                            if (logEnabled[5]) {
                                byte swap = tmp[parsePos + 1];
                                tmp[parsePos + 1] = tmp[parsePos];
                                tmp[parsePos] = swap;
                                gyroZ = BitConverter.ToInt16(tmp, parsePos);
                                gyroZ /= gDiv;

                                parsePos += 2;
                            }
                            if (logEnabled[6]) {
                                byte swap = tmp[parsePos + 1];
                                tmp[parsePos + 1] = tmp[parsePos];
                                tmp[parsePos] = swap;
                                temperature = BitConverter.ToInt16(tmp, parsePos);
                                temperature = temperature / 340 + 36.53;

                                parsePos += 2;
                            }
                            if (logEnabled[7]) {
                                byte swap = tmp[parsePos + 3];
                                tmp[parsePos + 3] = tmp[parsePos];
                                tmp[parsePos] = swap;
                                swap = tmp[parsePos + 2];
                                tmp[parsePos + 2] = tmp[parsePos + 1];
                                tmp[parsePos + 1] = swap;
                                roll = BitConverter.ToSingle(tmp, parsePos);

                                parsePos += 4;
                            }
                            if (logEnabled[8]) {
                                byte swap = tmp[parsePos + 3];
                                tmp[parsePos + 3] = tmp[parsePos];
                                tmp[parsePos] = swap;
                                swap = tmp[parsePos + 2];
                                tmp[parsePos + 2] = tmp[parsePos + 1];
                                tmp[parsePos + 1] = swap;
                                pitch = BitConverter.ToSingle(tmp, parsePos);

                                parsePos += 4;
                            }
                            if (logEnabled[9]) {
                                byte swap = tmp[parsePos + 3];
                                tmp[parsePos + 3] = tmp[parsePos];
                                tmp[parsePos] = swap;
                                swap = tmp[parsePos + 2];
                                tmp[parsePos + 2] = tmp[parsePos + 1];
                                tmp[parsePos + 1] = swap;
                                yaw = BitConverter.ToSingle(tmp, parsePos);

                                parsePos += 4;
                            }
                            if (logEnabled[10]) {
                                byte swap = tmp[parsePos + 1];
                                tmp[parsePos + 1] = tmp[parsePos];
                                tmp[parsePos] = swap;
                                throttle = BitConverter.ToUInt16(tmp, parsePos);

                                if (throttle != 0)
                                    throttle -= 1000;

                                parsePos += 2;
                            }

                            byte tmpSwap = tmp[parsePos + 1];
                            tmp[parsePos + 1] = tmp[parsePos];
                            tmp[parsePos] = tmpSwap;
                            deltaT = BitConverter.ToUInt16(tmp, parsePos);

                            parsePos += 2;

                            queue.Enqueue(new Data() { AccelX = accelX, AccelY = accelY, AccelZ = accelZ, GyroX = gyroX, GyroY = gyroY, GyroZ = gyroZ, Temperature = temperature, Roll = roll, Pitch = pitch, Yaw = yaw, Throttle = throttle, DeltaT = deltaT });
                        }
                        else {
                            Console.WriteLine("CRC error; their {0}, our {1}", crc, localCrc);
                        }
                    }
                }
            });

            while (true) ;
        }
    }
}
