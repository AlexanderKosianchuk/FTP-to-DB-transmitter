using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceProcess;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Collections.Specialized;
using System.Threading;

namespace ftpFastServerService
{
    public partial class ftpFastServerService : ServiceBase
    {
        ftpServer f = null;

        public ftpFastServerService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            ftpServer f = new ftpServer();
            f.Proccess();
        }

        protected override void OnStop()
        {
            f.Dispose();
        }

        public void AddLog(string log)
        {
            try
            {
                if (!EventLog.SourceExists("MyExampleService"))
                {
                    EventLog.CreateEventSource("MyExampleService", "MyExampleService");
                }
                eventLog1.Source = "MyExampleService";
                eventLog1.WriteEntry(log);
            }
            catch { }

        }
    }

    class ftpServer
    {
        static int streamPackLenght = 1024;
        private static AutoResetEvent eventTimeOut = new AutoResetEvent(true);
        static object threadNetwStr = new object();//to use networkstream in thread
        static int bytesReadFromNetwStr = new int();//to return bytesRead from streamRead thread 
        static byte[] bytesArrNetwStr = new byte[streamPackLenght];//to return bytesArr from streamRead thread 
        static bool connectionStabile = true;//to restart ftp
        static int frameCount = 0;
        static int dataPortNum = 1;

        TcpClient client = null;
        TcpClient clientData = null;

        public void Dispose()
        {
            if (client != null)
            {
                client.Close();
            }

            if (clientData != null)
            {
                clientData.Close();
            }        
        }

        public void Proccess()
        {
            string fileName = @"C:\ftp\file.dat";
            int frameLength = 77 * 2;

            string user = "anonymous";
            string pass = "I9523CB";

            int transferedBytesCount = 0;

            int flightId = this.CreateFlight();

            while (true)
            {
                if (client != null)
                {
                    client.Close();
                }

                if (clientData != null)
                {
                    clientData.Close();
                }

                List<byte> globalDataArray = new List<byte>();

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

                while (connectionStabile)
                {
                    answ = this.ReceiveCmd(stream);//SIZE 29_12_2013_02_35_50.bin
                    if (transferedBytesCount == 0)
                    {
                        this.SendCmd("550 File not found\r\n", stream);
                    }
                    else
                    {
                        this.SendCmd("213 " + transferedBytesCount + "\r\n", stream);
                    }
                    answ = this.ReceiveCmd(stream);//PASV

                    TcpListener serverData = null;

                    string portAnsw = "193,178,34,196,6," + dataPortNum.ToString();

                    this.SendCmd("227 Entering Passive Mode (" + portAnsw + ")\r\n", stream);
                    this.OpenDataPort(portAnsw, ref serverData);
                    dataPortNum += 1;
                    answ = this.ReceiveCmd(stream);//APPE 29_12_2013_02_35_50.bin

                    while (clientData == null)
                    {
                        try
                        {
                            clientData = serverData.AcceptTcpClient();
                        }
                        catch (Exception e)
                        {
                            // Let the user know what went wrong.
                            Console.WriteLine("WebClient error {0} {1}" + clientData.ToString(), e.Message);
                        }
                    }
                    Console.WriteLine("Data port connected!");

                    this.SendCmd("150 Connection accepted\r\n", stream);
                    NetworkStream streamData = clientData.GetStream();

                    int timeCounter = 0;
                    int maxTime = 5;
                    int readBytesCount = 0;

                    while ((timeCounter < maxTime) && connectionStabile)
                    {
                        threadNetwStr = streamData;
                        Thread tReceiveData = new Thread(ReceiveData);
                        Thread tTimeout = new Thread(TimerGo);

                        tReceiveData.Start();
                        tTimeout.Start();

                        eventTimeOut.WaitOne();

                        if (connectionStabile)
                        {
                            tTimeout.Abort();
                        }
                        else
                        {
                            tReceiveData.Abort();
                        }

                        byteAnsw = bytesArrNetwStr;
                        readBytesCount = bytesReadFromNetwStr;

                        if (readBytesCount > 0)
                        {
                            byte[] byteAnswPrep = new byte[readBytesCount];
                            Array.Copy(byteAnsw, byteAnswPrep, readBytesCount);
                            globalDataArray.AddRange(byteAnswPrep);
                            if (globalDataArray.Count > frameLength)
                            {
                                //insertFrame
                                frameCount++;
                                byte[] frame = new byte[frameLength];
                                for (int i = 0; i < frameLength; i++)
                                {
                                    frame[i] = globalDataArray[i];
                                }
                                globalDataArray.RemoveRange(0, frameLength);
                                this.InsertFrame(frame, flightId, frameCount);

                                Console.WriteLine(System.Text.Encoding.UTF8.GetString(frame));
                                Console.WriteLine(Environment.NewLine);
                            }
                            timeCounter = 0;
                            transferedBytesCount += readBytesCount;
                        }
                        else
                        {
                            timeCounter++;
                            Console.WriteLine("TimeCounter " + timeCounter);
                        }
                    }
                    this.SendCmd("226 Transfer OK\r\n", stream);
                    serverData.Stop();
                    clientData.Close();
                }

                client.Close();
            }
        }

        public int CreateFlight()
        {
            string baseUrl = "http://luch.local:8080/asyncRealTimeProcessor.php";

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

        public void InsertFrame(byte[] frame, int flightId, int frameNum)
        {
            string baseUrl = "http://luch.local:8080/asyncRealTimeProcessor.php";

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
                    //just restart
                    client.Close();
                    this.Proccess();
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
            byte[] bytes = new byte[1024];

            int i = stream.Read(bytes, 0, bytes.Length);
            // Translate data bytes to a ASCII string.
            string data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
            Console.WriteLine("Received: {0} ", data);
            return data;
        }

        public void WaitConnection(ref TcpClient client)
        {
            Int32 port = 21;
            IPAddress localAddr = IPAddress.Parse("193.178.34.196");
            //IPAddress localAddr = IPAddress.Parse("127.0.0.1");
            TcpListener server = new TcpListener(localAddr, port);

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
                    if (client != null)
                    {
                        client.Close();
                    }

                    if (clientData != null)
                    {
                        clientData.Close();
                    }

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
            int readBytesCount = 0;

            try
            {
                readBytesCount = stream.Read(bytes, 0, bytes.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine("Nothing worry about, just thread stream.Read because async {0}", e);
                this.Proccess();
            }
            //Console.WriteLine("Data received: {0} . Length {1}", data, readBytesCount);
            //Console.WriteLine(Environment.NewLine);

            bytesArrNetwStr = bytes;
            bytesReadFromNetwStr = readBytesCount;

            eventTimeOut.Set();

        }

        public void TimerGo()
        {
            Thread.Sleep(4000);
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
