using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Threading;

namespace ftpFastServer
{
    class Program
    {
        static void Main(string[] args)
        {
            ftpServer fs = new ftpServer();
            fs.Proccess();
        }
    }

    class ftpServer
    {
        const int streamPackLenght = 8192;
        const int frameLength = 77 * 2;

        static string ip = "91.218.212.137";
        static string baseSiteAddr = "http://91.218.212.137:80";

        private static AutoResetEvent eventTimeOut = new AutoResetEvent(true);
        static object threadNetwStr = new object();//to use networkstream in thread
        static int bytesReadFromNetwStr = new int();//to return bytesRead from streamRead thread 
        static byte[] bytesArrNetwStr = new byte[streamPackLenght];//to return bytesArr from streamRead thread
        static byte[] frameBuffer = new byte[frameLength];//to return bytesArr from streamRead thread
        static bool connectionStabile = true;//to restart ftp
        static int frameCount = 0;
        static int dataPortNum = 4;

        static List<byte> globalDataArray = new List<byte>();
        static int transferedBytesCount = 0;

        int flightId = 0;

        TcpClient client = null;
        TcpClient clientData = null;

        TcpListener server = null;
        TcpListener serverData = null;

        public void Proccess()
        {
            string fileName = @"C:\ftp\file.dat";

            string user = "anonymous";
            string pass = "I9523CB";

            flightId = this.CreateFlight();

            while (true)
            {
                String answ = String.Empty;

                byte[] byteAnsw = new byte[streamPackLenght];
                Console.Write("Waiting for a connection... ");

                this.WaitConnection(ref client);
                
                Console.WriteLine("Connected!");

                // Get a stream object for reading and writing
                NetworkStream stream = client.GetStream();
                this.SendCmd("220 FTP Ready\r\n", stream);
                answ = this.ReceiveCmd(stream);//USER anonymous
                this.SendCmd("331 Password required for anonymous\r\n", stream);
                answ = this.ReceiveCmd(stream);//PASS *******
                this.SendCmd("230 Logged on\r\n", stream);
                answ = this.ReceiveCmd(stream);//TYPE I
                this.SendCmd("200 Type set to I\r\n", stream);
                answ = this.ReceiveCmd(stream);//CWD SVM-1980
                this.SendCmd("250 CWD successful. \"/SVM-1980\" is current directory.\r\n", stream);
                connectionStabile = true;

                while (connectionStabile)
                {
                    answ = this.ReceiveCmd(stream);//SIZE 29_12_2013_02_35_50.bin
                    if (frameCount == 0)
                    {
                        this.SendCmd("550 File not found\r\n", stream);
                    }
                    else
                    {
                        this.SendCmd("213 " + (frameCount * frameLength) + "\r\n", stream);
                        globalDataArray.Clear();
                    }
                    answ = this.ReceiveCmd(stream);//PASV

                    string portAnsw = ip.Replace(".", ",") + ",6," + dataPortNum.ToString();

                    this.SendCmd("227 Entering Passive Mode (" + portAnsw + ")\r\n", stream);
                    this.OpenDataPort(portAnsw, ref serverData);
                    dataPortNum += 1;
                    answ = this.ReceiveCmd(stream);//APPE 29_12_2013_02_35_50.bin


                    try
                    {
                        clientData = serverData.AcceptTcpClient();
                    }
                    catch (Exception e)
                    {
                        // Let the user know what went wrong.
                        Console.WriteLine("WebClient error {0} {1}" + clientData.ToString(), e.Message);
                    }

                    if (clientData != null)
                    {
                       Console.WriteLine("Data port connected!");

                        this.SendCmd("150 Connection accepted\r\n", stream);
                        NetworkStream streamData = clientData.GetStream();

                        int timeCounter = 0;
                        int maxTime = 10;

                        while ((timeCounter < maxTime) && connectionStabile)
                        {
                            threadNetwStr = streamData;
                            Thread tReceiveData = new Thread(ReceiveData);
                            Thread tTimeout = new Thread(TimerGo);
                            Thread tFrameInsert = new Thread(PreparaFrameAndTransfer);

                            tReceiveData.Start();
                            tTimeout.Start();
                            tFrameInsert.Start();

                            eventTimeOut.WaitOne();

                            if (connectionStabile)
                            {
                                tTimeout.Abort();
                            }
                            else
                            {
                                tReceiveData.Abort();
                                tFrameInsert.Abort();
                                tTimeout.Abort();

                                Console.WriteLine("Connection unstabile. Restart");
                            }

                            if (transferedBytesCount == 0)
                            {
                                timeCounter++;
                                Console.WriteLine("TimeCounter " + timeCounter);
                            }
                            else
                            {
                                timeCounter = 0;
                            }
                        }
                        this.SendCmd("226 Transfer OK\r\n", stream);
                        serverData.Stop();
                        clientData.Close();
                    }
                }

                serverData.Stop();
                clientData.Close();
                server.Stop();
                client.Close();  
            }
        }

        public int CreateFlight()
        {
            string baseUrl = baseSiteAddr + "/asyncRealTimeProcessor.php";

            NameValueCollection nvc = new NameValueCollection();
            nvc.Add("action", "create");
            nvc.Add("bort", "1");
            nvc.Add("voyage", "1");
            nvc.Add("copyCreationTime", "00:00");
            nvc.Add("copyCreationDate", "1-1-2020");
            nvc.Add("bruType", "LUCH-12-IRIDIUM_MI8");
            nvc.Add("performer", "q");
            nvc.Add("centring", "1");
            nvc.Add("engines", "1");

            byte[] responseArray;
            int flightId = 0;
            using (var client = new WebClient())
            {
                responseArray = client.UploadValues(baseUrl, nvc);

                string responseSting = System.Text.Encoding.UTF8.GetString(responseArray);
                flightId = Convert.ToInt32(responseSting.Trim());
            }
            return flightId;
        }

        public void PreparaFrameAndTransfer()
        {
            while (globalDataArray.Count > frameLength * 2)
            {
                //insertFrame
                if ((globalDataArray[0] == 77) && (globalDataArray[1] == 105) &&
                    (globalDataArray[frameLength] == 77) && (globalDataArray[frameLength + 1] == 105))
                {
                    byte[] frame = new byte[frameLength];
                    for (int i = 0; i < frameLength; i++)
                    {
                        frame[i] = globalDataArray[i];
                    }
                    frameBuffer = frame;
                    lock (globalDataArray)
                    {
                        globalDataArray.RemoveRange(0, frameLength);
                    }
                    
                    this.InsertFrame(frame, flightId, frameCount);

                    Console.WriteLine(System.Text.Encoding.UTF8.GetString(frame));
                    Console.WriteLine(Environment.NewLine);
                    frameCount++;
                }
                else if ((globalDataArray[0] == 77) && (globalDataArray[1] == 105) &&
                    (globalDataArray[frameLength] != 77) && (globalDataArray[frameLength + 1] != 105))
                {
                    int j = 0;
                    lock (globalDataArray)
                    {
                        globalDataArray.RemoveAt(0);
                        while ((globalDataArray[j] != 77) && (globalDataArray[j + 1] != 105) &&
                            (j < globalDataArray.Count - 3))
                        {
                            globalDataArray.RemoveAt(0);
                            j++;
                        }
                    }

                    int brokenFrames = j / frameLength + 1;

                    if (brokenFrames < 2)
                    {
                        for (int k = 0; k < brokenFrames; k++)
                        {
                            this.InsertFrame(frameBuffer, flightId, frameCount);
                            frameCount++;
                        }
                    }
                    else
                    {
                        globalDataArray.Clear();
                        connectionStabile = false;
                    }

                    Console.WriteLine("Broken next frames {0}", brokenFrames);
                }
                else
                {
                    int j = 0;
                    lock (globalDataArray)
                    {
                        while ((globalDataArray[j] != 77) && (globalDataArray[j + 1] != 105) &&
                            (globalDataArray[j + frameLength] != 77) && (globalDataArray[j + frameLength + 1] != 105) &&
                            (j < globalDataArray.Count - frameLength - 3))
                        {
                            globalDataArray.RemoveAt(0);
                            j++;
                        }
                    }

                    int brokenFrames = j / frameLength + 1;

                    if (brokenFrames < 2)
                    {
                        for (int k = 0; k < brokenFrames; k++)
                        {
                            this.InsertFrame(frameBuffer, flightId, frameCount);
                            frameCount++;
                        }
                    }
                    else
                    {
                        globalDataArray.Clear();
                        connectionStabile = false;
                    }

                    Console.WriteLine("Broken frames {0}", brokenFrames);
                }
            }        
        }

        public void InsertFrame(byte[] frame, int flightId, int frameNum)
        {
            string baseUrl = baseSiteAddr + "/asyncRealTimeProcessor.php";

            NameValueCollection nvc = new NameValueCollection();

            nvc.Add("action", "append");
            nvc.Add("flightId", flightId.ToString());

            string hex = BitConverter.ToString(frame).Replace("-", string.Empty);
            //hex = System.Text.Encoding.UTF8.GetString(frame);
            nvc["frame"] = hex;
            nvc["frameNum"] = frameNum.ToString();
            using (var webClient = new WebClient())
            {
                try
                {
                    webClient.UploadValues(baseUrl, nvc);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Nothing worry about, just client.UploadValues exc {0} ", e);
                    connectionStabile = false;
                    eventTimeOut.Set();
                }
            }
        }

        public string FormFolderList()
        {
            //send folder filesList
            string Path = @"C:\WebServer\Apache24\bin";
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
                return strFilesList;
            }
            catch
            {
                return "false";
            }
        }

        public void SendCmd(String text, NetworkStream stream)
        {
            byte[] msg = System.Text.Encoding.ASCII.GetBytes(text);
            stream.Write(msg, 0, msg.Length);
            Console.WriteLine("Sent: {0}", text);
        }

        public string ReceiveCmd(NetworkStream stream)
        {
            //receive
            byte[] bytes = new byte[streamPackLenght];
            
            int i = stream.Read(bytes, 0, bytes.Length);
            // Translate data bytes to a ASCII string.
            string data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
            Console.WriteLine("Received: {0} ", data);
            return data;
        }

        public void WaitConnection(ref TcpClient client)
        {
            Int32 port = 21;
            IPAddress localAddr = IPAddress.Parse(ip);
            //IPAddress localAddr = IPAddress.Parse("127.0.0.1");
            server = new TcpListener(localAddr, port);

            // Start listening for client requests.
            // Perform a blocking call to accept requests.
            // You could also user server.AcceptSocket() here.
            try
            {
                server.Start();
                client = server.AcceptTcpClient();
            }
            catch (Exception e)
            {
                // Let the user know what went wrong.
                Console.WriteLine("WebClient error {0} ", e.Message);
                serverData.Stop();
                clientData.Close();
                server.Stop();
                client.Close();  
                this.Proccess();
            }
        }

        public void OpenDataPort(string dynamicIp, ref TcpListener serverData)
        {
            string[] IP_Parts = dynamicIp.Split(',');

            if (IP_Parts.Length == 6)
            {
                string clientIP = IP_Parts[0] + "." + IP_Parts[1] + "." + IP_Parts[2] + "." + IP_Parts[3];
                IPAddress IP = IPAddress.Parse(clientIP);
                int tmpPort = (Convert.ToInt32(IP_Parts[4]) << 8) | Convert.ToInt32(IP_Parts[5]);

                try
                {
                    serverData = new TcpListener(IP, tmpPort);
                    serverData.Start();
                    Console.WriteLine("ServerData start OK");
                }
                catch (Exception e)
                {
                    // Let the user know what went wrong.
                    Console.WriteLine("WebClient error {0} ", e);
                    dataPortNum++;
                    serverData.Stop();
                    clientData.Close();
                    server.Stop();
                    client.Close();  
                    
                    this.Proccess();
                }
            }
        }

        public void ReceiveData()
        {
            NetworkStream stream = (NetworkStream)threadNetwStr;
            //receive
            string data = string.Empty;
            byte[] bytes = new byte[streamPackLenght];

            try
            {
                transferedBytesCount = stream.Read(bytes, 0, bytes.Length);
                byte[] byteAnswPrep = new byte[transferedBytesCount];
                Array.Copy(bytes, byteAnswPrep, transferedBytesCount);
                lock (globalDataArray)
                {
                    globalDataArray.AddRange(byteAnswPrep);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Nothing worry about, just thread stream.Read because async {0}", e);
                connectionStabile = false;
                eventTimeOut.Set();
            }
            //Console.WriteLine("Data received: {0} . Length {1}", data, transferedBytesCount);
            //Console.WriteLine(Environment.NewLine);

            eventTimeOut.Set();
        }

        public void TimerGo()
        {
            Thread.Sleep(20000);
            connectionStabile = false;
            eventTimeOut.Set();       
        }

        public void SendData(NetworkStream stream, string msg)
        {
            //receive
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(msg);
            stream.Write(bytes, 0, bytes.Length);
            Console.WriteLine("Data sent: {0}", msg);
        }
    }
}

