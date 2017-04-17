using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ftpFastServer
{
    class Program
    {
        static void Main(string[] args)
        {
            string fileName = @"C:\Users\Sasha Kos\Desktop\23_12_2013_20_56_31.bin";
            int frameLength = 77 * 2;
            string baseUrl = "http://www.luch.com/asyncRealTimeProcessor.php";

            TcpListener server = null;
            TcpListener serverData = null;

            try
            {
                // Set the TcpListener on port 21.
                Int32 port = 21;
                IPAddress localAddr = IPAddress.Parse("127.0.0.1");
                String StartUpDirectory = "C:/WebServer/ftp";

                // TcpListener server = new TcpListener(port);
                server = new TcpListener(localAddr, port);
                // Start listening for client requests.
                server.Start();
                
                // Buffer for reading data
                Byte[] bytes = new Byte[256];
                String data = null;
                Console.Write("Waiting for a connection... ");

                // Perform a blocking call to accept requests.
                // You could also user server.AcceptSocket() here.
                TcpClient client = server.AcceptTcpClient();
                TcpClient clientData;
                Console.WriteLine("Connected!");
                
                data = null;
                // Get a stream object for reading and writing
                NetworkStream stream = client.GetStream();
                
                //send
                String text = "220 FTP Ready\r\n";
                byte[] msg = System.Text.Encoding.ASCII.GetBytes(text);
                stream.Write(msg, 0, msg.Length);
                Console.WriteLine("Sent: {0}", text);

                //receive
                int i = stream.Read(bytes, 0, bytes.Length);
                // Translate data bytes to a ASCII string.
                data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                Console.WriteLine("Received: {0}", data);

                //send
                text = "331 Password required for account\r\n";
                msg = System.Text.Encoding.ASCII.GetBytes(text);
                stream.Write(msg, 0, msg.Length);
                Console.WriteLine("Sent: {0}", text);

                //receive
                i = stream.Read(bytes, 0, bytes.Length);
                data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                Console.WriteLine("Received: {0}", data);

                //send
                text = "230 Logged on\r\n";
                msg = System.Text.Encoding.ASCII.GetBytes(text);
                stream.Write(msg, 0, msg.Length);
                Console.WriteLine("Sent: {0}", text);

                //receive
                i = stream.Read(bytes, 0, bytes.Length);
                data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                Console.WriteLine("Received: {0}", data);

                //send
                text = "215 Windows_NT\r\n";
                msg = System.Text.Encoding.ASCII.GetBytes(text);
                stream.Write(msg, 0, msg.Length);
                Console.WriteLine("Sent: {0}", text);

                //receive
                i = stream.Read(bytes, 0, bytes.Length);
                data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                Console.WriteLine("Received: {0}", data);

                //send
                text = "211-Features:\r\nMDTM\r\nREST STREAM\r\nSIZE\r\nMLST\r\nMLSD\r\nUTF8\r\nCLNT\r\nMFMT\r\n211 End\r\n";
                msg = System.Text.Encoding.ASCII.GetBytes(text);
                stream.Write(msg, 0, msg.Length);
                Console.WriteLine("Sent: {0}", text);

                //receive
                i = stream.Read(bytes, 0, bytes.Length);
                data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                Console.WriteLine("Received: {0}", data);

                //send
                text = "200 Don't care.\r\n";
                msg = System.Text.Encoding.ASCII.GetBytes(text);
                stream.Write(msg, 0, msg.Length);
                Console.WriteLine("Sent: {0}", text);

                //receive
                i = stream.Read(bytes, 0, bytes.Length);
                data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                Console.WriteLine("Received: {0}", data);
                
                //send
                text = "202 UTF8 mode is always enabled. No need to send this command.\r\n";
                msg = System.Text.Encoding.ASCII.GetBytes(text);
                stream.Write(msg, 0, msg.Length);
                Console.WriteLine("Sent: {0}", text);

                //receive
                i = stream.Read(bytes, 0, bytes.Length);
                data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                Console.WriteLine("Received: {0}", data);

                //send
                text = "257 \"/\" is current directory.\r\n";
                msg = System.Text.Encoding.ASCII.GetBytes(text);
                stream.Write(msg, 0, msg.Length);
                Console.WriteLine("Sent: {0}", text);

                //receive
                i = stream.Read(bytes, 0, bytes.Length);
                data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                Console.WriteLine("Received: {0}", data);

                //send
                text = "200 Type set to A\r\n";
                msg = System.Text.Encoding.ASCII.GetBytes(text);
                stream.Write(msg, 0, msg.Length);
                Console.WriteLine("Sent: {0}", text);

                //receive
                i = stream.Read(bytes, 0, bytes.Length);
                data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                Console.WriteLine("Received: {0}", data);
                string[] IP_Parts = data.Split(new char[] { ' ', ','});
                if (IP_Parts.Length != 7)
                {
                    //send
                    text = "550 Invalid arguments.\r\n";
                    msg = System.Text.Encoding.ASCII.GetBytes(text);
                    stream.Write(msg, 0, msg.Length);
                    Console.WriteLine("Sent: {0}", text);
                    return;
                }
                else
                {
                    string ClientIP = IP_Parts[1] + "." + IP_Parts[2] + "." + IP_Parts[3] + "." + IP_Parts[4];
                    int tmpPort = (Convert.ToInt32(IP_Parts[5]) << 8) | Convert.ToInt32(IP_Parts[6]);
                    IPHostEntry hostEntry = Dns.GetHostEntry(ClientIP);
                    String host = hostEntry.HostName;
                    Console.WriteLine("Host: {0}", host);
                    clientData = new TcpClient(host, tmpPort);

                    //send
                    text = "200 Port command successful\r\n";
                    msg = System.Text.Encoding.ASCII.GetBytes(text);
                    stream.Write(msg, 0, msg.Length);
                    Console.WriteLine("Sent: {0}", text);                
                }

                //receive
                i = stream.Read(bytes, 0, bytes.Length);
                data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                Console.WriteLine("Received: {0}", data);

                //send
                text = "150 Opening data channel for directory listing of \" / \" \r\n";
                msg = System.Text.Encoding.ASCII.GetBytes(text);
                stream.Write(msg, 0, msg.Length);
                Console.WriteLine("Sent: {0}", text);    

                NetworkStream streamData = clientData.GetStream();
                //send folder filesList
                string Path = StartUpDirectory + "/";
                try
                {
                    string[] FilesList = Directory.GetFiles(Path, "*.*", SearchOption.TopDirectoryOnly);
                    string[] FoldersList = Directory.GetDirectories(Path, "*.*", SearchOption.TopDirectoryOnly);
                    string strFilesList = "";

                    foreach (string Folder in FoldersList)
                    {
                        string date = Directory.GetCreationTime(Folder).ToString("MM-dd-yy hh:mm:tt");
                        strFilesList += date + " <DIR> " + Folder.Substring(Folder.Replace('\\', '/').LastIndexOf('/') + 1) + "\r\n";
                    }

                    foreach (string FileName in FilesList)
                    {
                        string date = File.GetCreationTime(FileName).ToString("MM-dd-yy hh:mm:tt");
                        strFilesList += date + " " + new FileInfo(FileName).Length.ToString() + " " + FileName.Substring(FileName.Replace('\\', '/').LastIndexOf('/') + 1) + "\r\n";
                    }

                    //send
                    text = strFilesList;
                    msg = System.Text.Encoding.ASCII.GetBytes(text);
                    streamData.Write(msg, 0, msg.Length);
                    Console.WriteLine("Sent: {0}", text);                    
                }
                catch (DirectoryNotFoundException)
                {
                    //send
                    text = "550 Invalid path specified.\r\n";
                    msg = System.Text.Encoding.ASCII.GetBytes(text);
                    stream.Write(msg, 0, msg.Length);
                    Console.WriteLine("Sent: {0}", text); 
                }
                catch
                {
                    text = "426 Connection closed; transfer aborted.\r\n";
                    msg = System.Text.Encoding.ASCII.GetBytes(text);
                    stream.Write(msg, 0, msg.Length);
                    Console.WriteLine("Sent: {0}", text);   

                }
                finally
                {
                    //send
                    text = "226 Successfully transferred \"/\"\r\n";
                    msg = System.Text.Encoding.ASCII.GetBytes(text);
                    stream.Write(msg, 0, msg.Length);
                    Console.WriteLine("Sent: {0}", text);
                }            
                
                // Shutdown and end connection
                clientData.Close();

                //receive
                i = stream.Read(bytes, 0, bytes.Length);
                data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                Console.WriteLine("Received: {0}", data);

            }
            catch(SocketException e)
            {
                  Console.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                // Stop listening for new clients.
                server.Stop();
            }
            Console.WriteLine("\nHit enter to continue...");
            Console.Read();

            /*    try
                {
                    NameValueCollection nvc = new NameValueCollection();
                    nvc.Add("action", "create");
                    nvc.Add("bort", "1");
                    nvc.Add("voyage", "1");
                    nvc.Add("copyCreationTime", "00:00");
                    nvc.Add("copyCreationDate", "1-1-2020");
                    nvc.Add("bruType", "LUCH-12-IRIDIUM_MI8");
                    nvc.Add("performer", "1");
                    nvc.Add("centring", "1");
                    nvc.Add("engines", "1");

                    byte[] responseArray;
                    using (var client = new WebClient())
                    {
                        responseArray = client.UploadValues(baseUrl, nvc);

                        string responseSting = System.Text.Encoding.UTF8.GetString(responseArray);
                        int flightId = Convert.ToInt32(responseSting.Trim());
                        Console.WriteLine(flightId);
                        nvc.Clear();

                        int frameNum = 0;
                        nvc.Add("frameNum", frameNum.ToString());
                        nvc.Add("action", "append");
                        nvc.Add("flightId", flightId.ToString());

                        // Create an instance of StreamReader to read from a file. 
                        // The using statement also closes the StreamReader. 
                        using (BinaryReader br = new BinaryReader(File.Open(fileName, FileMode.Open)))
                        {
                            byte[] frame = new byte[frameLength];
                            // Read and display lines from the file until the end of  
                            // the file is reached. 
                            while ((frame = br.ReadBytes(frameLength)) != null)
                            {
                                string hex = BitConverter.ToString(frame).Replace("-", string.Empty);
                                //hex = System.Text.Encoding.UTF8.GetString(frame);
                                nvc["frame"] = hex;
                                nvc["frameNum"] = frameNum.ToString();
                                client.UploadValues(baseUrl, nvc);
                                Console.WriteLine(hex + Environment.NewLine + Environment.NewLine);
                                frameNum++;
                                Thread.Sleep(1000);
                            }
                        }
                    }

                }
                catch (Exception e)
                {
                    // Let the user know what went wrong.
                    Console.WriteLine("WebClient error ");
                    Console.WriteLine(e.Message);
                }

                Console.ReadKey();
            }
            catch (Exception e)
            {
                // Let the user know what went wrong.
                Console.WriteLine("WebClient error ");
                Console.WriteLine(e.Message);
            }*/
        }

    }
}
