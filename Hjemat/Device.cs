using System;
using System.IO.Ports;
using System.Collections.Generic;

namespace Hjemat
{
    public class Device
    {
        public byte deviceID;
        public int productID;
        public Dictionary<byte, short> values;

        public static SerialPort serialPort;

        public Device(byte deviceID = 0x0, int productID = 0x0, Dictionary<byte, Int16> values = null)
        {
            this.deviceID = deviceID;
            this.productID = productID;

            this.values = values ?? new Dictionary<byte, Int16>();
        }

        public bool SendMessage(CommandID command, byte byte1, byte byte2, byte byte3)
        {
            byte?[] data = new byte?[3];

            data[0] = byte1;
            data[1] = byte2;
            data[2] = byte3;

            var message = new Message(deviceID, command, data);
            message.Send();

            return true;
        }

        public bool SendMessage(CommandID command, byte byte1, int data)
        {
            var dataBytes = BitConverter.GetBytes(data);

            return SendMessage(command, byte1, dataBytes[1], dataBytes[0]);
        }

        public bool SendMessage(CommandID command, int data)
        {
            var dataBytes = BitConverter.GetBytes(data);

            return SendMessage(command, dataBytes[2], dataBytes[1], dataBytes[0]);
        }

        public short GetValue(byte dataID)
        {
            SendMessage(CommandID.Get, dataID, 0);

            try
            {
                var response = Message.Read();

                var expectedHeader = Message.CreateHeader(deviceID, CommandID.Return);

                if (response.GetHeader() == expectedHeader && response.bytes[1] == dataID)
                {
                    return response.GetShortData();
                }

                throw new System.Exception($"Unexpected header {response.GetHeader().ToString()}");
            }
            catch (System.TimeoutException) {
                Console.WriteLine("Something took a little too long");
                return 0;
            }



        }

        public void SetupValues(Product product)
        {
            this.values = new Dictionary<byte, short>();

            foreach (var productValue in product.values)
            {
                this.values.Add(productValue.id, this.GetValue(productValue.id));
            }
        }
        /*
                public bool SendData(byte dataID, int data)
                {
                    SendMessage(Command.Set, dataID, data);

                    byte[] confirmationMessage = new byte[4];
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();

                    bool reading = true;
                    int messageIndex = 0;

                    while (reading)
                    {
                        var numBytes = serialPort.BytesToRead;
                        if (numBytes < 1)
                            continue;

                        serialPort.Read(confirmationMessage, messageIndex, numBytes);
                        messageIndex += numBytes;

                        if (messageIndex >= 3)
                        {
                            reading = false;
                        }
                    }

                    var expectedHeader = Message.CreateHeader(deviceID, Command.Return);

                    if (confirmationMessage[0] == expectedHeader)
                    {
                        Console.WriteLine("Data confirmed received");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("No confirmation of reception");
                        foreach (var part in confirmationMessage)
                        {
                            Console.Write(part);
                            Console.Write(" ");
                        }
                        Console.WriteLine("");

                        return false;
                    }
                }
                */
    }
}