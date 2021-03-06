﻿using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Diagnostics;
using Newtonsoft.Json;
using System.IO;
using RestSharp;
using Raspberry.IO.GeneralPurpose;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Linq;

namespace Hjemat
{
    class Program
    {
        int updateInterval = 1;
        static SerialPort serialPort = new SerialPort();
        static Config config = null;

        static string configFolderPath;

        static RestManager restManager;
        
        static bool isRunning = true;
        static bool isQuitting = false;

        ///<summary>
        ///Creates a config from the settings.json file in the settingsPath
        ///</summary>
        ///<param name="settingsPath">The path of the folder containing the settings file</param>
        static Config LoadSettings(string settingsPath)
        {
            // Creates a base config
            Config config = new Config(new Uri("http://127.0.0.1/api/"), new Config.SerialConfig("COM3"));
            string filePath;

            // Creates path to settings file
            filePath = Path.Combine(settingsPath, "settings.json");

            // If the settings file exists:
            if (File.Exists(filePath))
            {
                // We read the text in the settings file
                Console.WriteLine("Reading settings file...");
                var settingsFile = File.ReadAllText(filePath);

                // The text from the file is deserialized to an config object
                Console.WriteLine("Setting up according to settings.json...");
                config = JsonConvert.DeserializeObject<Config>(settingsFile);

            }
            else    // if the file doesn't exists we create it based on the standard config and then stops the program
            {       // so the user can edit it
                Console.WriteLine("Settings file not found. Creating standard settings file");
                File.WriteAllText(filePath, JsonConvert.SerializeObject(config, Formatting.Indented));

                Console.WriteLine($"Settings file created, needs configuration before using program.\nFile path: {filePath}");

                throw new System.ArgumentException("Settings file wasn't existing. Halting program to let user edit settings");
            }

            return config;
        }

        static Dictionary<int, Product> GetProductsDict(string settingsPath)
        {
            var products = new Dictionary<int, Product>();
            var productList = new List<Product>();

            var filePath = Path.Combine(
                settingsPath,
                "products.json");


            if (File.Exists(filePath))
            {
                Console.WriteLine("Reading products file...");
                var settingsFile = File.ReadAllText(filePath);

                Console.WriteLine("Setting up according to products.json...");
                productList = JsonConvert.DeserializeObject<List<Product>>(settingsFile);

                foreach (var product in productList)
                {
                    products.Add(product.id, product);
                }

            }

            return products;
        }

        public static void Delay(int miliseconds)
        {
            System.Threading.Thread.Sleep(miliseconds);
        }

        public static void Loop()
        {
            var cki = Console.ReadKey();

            if (cki.Key == ConsoleKey.Q || isQuitting)
            {
                if (!isQuitting)
                {
                    Console.Write("\nAre you sure you want to quit? (y/n): ");

                    isQuitting = true;
                }

                if (cki.Key == ConsoleKey.Y)
                {
                    Console.WriteLine();
                    isRunning = false;
                }
                else if (cki.Key == ConsoleKey.N)
                {
                    isQuitting = false;
                }
            }
        }


        static void Main(string[] args)
        {
            // Initializing Pin 11 which is the pin we are using to switch between read and write on the RS486 IC
            Message.rwPinConfig = ConnectorPin.P1Pin11.Output();
            Message.rwPinConnection = new GpioConnection(Message.rwPinConfig);

            // Getting path to folder with settings from command line argument
            // If there's no command line argument, we make a path based on the OS's preferences
            if (args[0] != null)
            {
                configFolderPath = args[0];
            }
            else 
            {
                configFolderPath = Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                    "hjemat-app");
            }
            

            // We create the path if it doesn't exists.
            if (!File.Exists(configFolderPath))
                Directory.CreateDirectory(args[0] ?? configFolderPath);

            // Loading settings from the settings file in the config path
            config = LoadSettings(configFolderPath);

            // If we failed to load the settings, we tell the user and exits
            if (config == null)
            {
                Console.WriteLine("Failed to load settings file");
                return;
            }

            // If the serial config in the settings are invalid we tell the user and exits
            if (config.serialConfig == null)
            {
                Console.WriteLine("Error getting SerialConfig from settings");
                return;
            }

            Console.WriteLine("Loaded settings");

            // Loads the products from the products file
            ProductsManager.products = GetProductsDict(configFolderPath);

            // Sets up the serial port according to the settings file
            // The serial port is what we use to communicate with RS485
            serialPort = config.CreateSerialPort();
            Message.serialPort = serialPort;
            Device.serialPort = serialPort;

            // Opens the serial port so we can use it.
            // If it fails to open the program exits and the error is shown to ther user.
            try
            {
                serialPort.Open();
            }
            catch (System.Exception exp)
            {
                Console.WriteLine($"Error opening serial port. Make sure device is connected and that {serialPort.PortName} is the correct port");

                Console.WriteLine($"Error message: {exp.Message}");

                return;
            }

            Console.WriteLine("Giving port time to open...");
            Delay(400);

            Console.WriteLine("");

            // Tries to ping all device ids, so we can find devices which already has been assigned an id
            DevicesManager.ScanDevices();

            // Gets values from devices
            DevicesManager.UpdateDevicesValues();

            // Sets up connection to the database
            restManager = new RestManager(config.serverUrl);

            // Synchronize devices with the database
            restManager.SynchronizeDevices(DevicesManager.devices);

            // Starts the WebSocket server
            var wssv = WebSocket.CreateServer();
            wssv.Start();
            Console.WriteLine($"Started WebSocket server on port {wssv.Port}");

            // Prepares for sending device data over WebSocket when devices are paired
            DevicesManager.OnDevicePaired += WebSocket.SendDevice;

            // Infinite loop to keep the program running, until isRunning is set to false
            while (isRunning)
            {
                Loop();
            }

            // Closes serial port, pin connection and websocket to clean up
            wssv.Stop();
            serialPort.Close();
            Message.rwPinConnection.Close();
        }
    }
}
