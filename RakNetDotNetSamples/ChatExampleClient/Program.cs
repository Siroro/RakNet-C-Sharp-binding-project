using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RakNet;
using System.Diagnostics;


namespace ChatExampleClient
{
    class Program
    {
        public const short AF_INET = 2;
        public const short AF_INET6 = 23;

        static void Main(string[] args)
        {
            RakNetStatistics rss = new RakNetStatistics();
            RakPeerInterface client = RakPeerInterface.GetInstance();
            Packet p = new Packet();
            byte packetIdentifier;
            bool isServer = false;

            SystemAddress ClientID = RakNet.RakNet.UNASSIGNED_SYSTEM_ADDRESS;

            string ip, serverPort, clientPort;

            Console.WriteLine("This is a sample implementation of a text based chat client");
            Console.WriteLine("Connect to the project 'Chat Example Server'");

            Console.WriteLine("Enter the client port to listen on");
            clientPort = Console.ReadLine();
            if (clientPort.Length == 0)
                clientPort = "0";

            Console.WriteLine("Enter the IP to connect to");
            ip = Console.ReadLine();
            if (ip.Length == 0)
                ip = "127.0.0.1";

            Console.WriteLine("Enter the port to connect to");
            serverPort = Console.ReadLine();
            if (serverPort.Length == 0)
                serverPort = "1234";

            SocketDescriptor socketDescriptor = new SocketDescriptor(Convert.ToUInt16(clientPort), "0");
            socketDescriptor.socketFamily = AF_INET;

            client.Startup(8, socketDescriptor, 1);
            client.SetOccasionalPing(true);

            ConnectionAttemptResult car = client.Connect(ip, Convert.ToUInt16(serverPort), "Rumpelstiltskin", "Rumpelstiltskin".Length);
            if (car != RakNet.ConnectionAttemptResult.CONNECTION_ATTEMPT_STARTED)
                throw new Exception();

            Console.WriteLine("My IP Addresses:");
            for (uint i = 0; i < client.GetNumberOfAddresses(); i++)
            {
                Console.WriteLine(client.GetLocalIP(i).ToString());
            }
            Console.WriteLine("My GUID is " + client.GetGuidFromSystemAddress(RakNet.RakNet.UNASSIGNED_SYSTEM_ADDRESS).ToString());
            Console.WriteLine("'quit' to quit. 'stat' to show stats. 'ping' to ping.\n'disconnect' to disconnect. 'connect' to reconnnect. Type to talk.");
            string message;
            while (true)
            {
                System.Threading.Thread.Sleep(30);

                //Entire networking is threaded
                if (Console.KeyAvailable)
                {
                    message = Console.ReadLine();
                    if (message == "quit")
                    {
                        Console.WriteLine("Quitting");
                        break;
                    }

                    if (message == "stat")
                    {
                        string message2 = "";
                        rss = client.GetStatistics(client.GetSystemAddressFromIndex(0));
                        RakNet.RakNet.StatisticsToString(rss, out message2, 2);
                        Console.WriteLine(message2);
                        continue;
                    }

                    if (message == "disconnect")
                    {
                        Console.WriteLine("Enter index to disconnect: ");
                        string str = Console.ReadLine();
                        if (str == "")
                            str = "0";
                        uint index = Convert.ToUInt32(str, 16);
                        client.CloseConnection(client.GetSystemAddressFromIndex(index), false);
                        Console.WriteLine("Disconnecting");
                        continue;
                    }

                    if (message == "shutdown")
                    {
                        client.Shutdown(100);
                        Console.WriteLine("Disconnecting");
                        continue;
                    }

                    if (message == "ping")
                    {
                        if (client.GetSystemAddressFromIndex(0) != RakNet.RakNet.UNASSIGNED_SYSTEM_ADDRESS)
                            client.Ping(client.GetSystemAddressFromIndex(0));
                        continue;
                    }
                    if (message == "connect")
                    {
                        Console.WriteLine("Enter the IP to connect to");
                        ip = Console.ReadLine();
                        if (ip.Length == 0)
                            ip = "127.0.0.1";

                        Console.WriteLine("Enter the port to connect to");
                        serverPort = Console.ReadLine();
                        if (serverPort.Length == 0)
                            serverPort = "1234";

                        ConnectionAttemptResult car2 = client.Connect(ip, Convert.ToUInt16(serverPort), "Rumpelstiltskin", "Rumpelstiltskin".Length);

                        continue;
                    }
                    if (message == "getlastping")
                    {
                        if (client.GetSystemAddressFromIndex(0) != RakNet.RakNet.UNASSIGNED_SYSTEM_ADDRESS)
                            Console.WriteLine(client.GetLastPing(client.GetSystemAddressFromIndex(0)));

                        continue;
                    }

                    if (message.Length > 0)
                        client.Send(message, message.Length + 1, PacketPriority.HIGH_PRIORITY, PacketReliability.RELIABLE_ORDERED, (char)0, RakNet.RakNet.UNASSIGNED_SYSTEM_ADDRESS, true);
                }

                for (p = client.Receive(); p != null; client.DeallocatePacket(p), p = client.Receive())
                {
                    packetIdentifier = GetPacketIdentifier(p);
                    switch ((DefaultMessageIDTypes)packetIdentifier)
                    {
                        case DefaultMessageIDTypes.ID_DISCONNECTION_NOTIFICATION:
                            Console.WriteLine("ID_DISCONNECTION_NOTIFICATION");
                            break;
                        case DefaultMessageIDTypes.ID_ALREADY_CONNECTED:
                            Console.WriteLine("ID_ALREADY_CONNECTED with guid " + p.guid);
                            break;
                        case DefaultMessageIDTypes.ID_INCOMPATIBLE_PROTOCOL_VERSION:
                            Console.WriteLine("ID_INCOMPATIBLE_PROTOCOL_VERSION ");
                            break;
                        case DefaultMessageIDTypes.ID_REMOTE_DISCONNECTION_NOTIFICATION:
                            Console.WriteLine("ID_REMOTE_DISCONNECTION_NOTIFICATION ");
                            break;
                        case DefaultMessageIDTypes.ID_REMOTE_CONNECTION_LOST: // Server telling the clients of another client disconnecting forcefully.  You can manually broadcast this in a peer to peer enviroment if you want.
                            Console.WriteLine("ID_REMOTE_CONNECTION_LOST");
                            break;
                        case DefaultMessageIDTypes.ID_CONNECTION_BANNED: // Banned from this server
                            Console.WriteLine("We are banned from this server.\n");
                            break;			
                        case DefaultMessageIDTypes.ID_CONNECTION_ATTEMPT_FAILED:
                            Console.WriteLine("Connection attempt failed ");
                            break;
                        case DefaultMessageIDTypes.ID_NO_FREE_INCOMING_CONNECTIONS:
                            Console.WriteLine("Server is full ");
                            break;
                        case DefaultMessageIDTypes.ID_INVALID_PASSWORD:
                            Console.WriteLine("ID_INVALID_PASSWORD\n");
                            break;
                        case DefaultMessageIDTypes.ID_CONNECTION_LOST:
                            // Couldn't deliver a reliable packet - i.e. the other system was abnormally
                            // terminated
                            Console.WriteLine("ID_CONNECTION_LOST\n");
                            break;
                        case DefaultMessageIDTypes.ID_CONNECTION_REQUEST_ACCEPTED:
                            // This tells the client they have connected
                            Console.WriteLine("ID_CONNECTION_REQUEST_ACCEPTED to %s " + p.systemAddress.ToString() + "with GUID " + p.guid.ToString());
                            Console.WriteLine("My external address is:"  + client.GetExternalID(p.systemAddress).ToString());
                            break;
                        case DefaultMessageIDTypes.ID_CONNECTED_PING:
                        case DefaultMessageIDTypes.ID_UNCONNECTED_PING:
                            Console.WriteLine("Ping from " + p.systemAddress.ToString(true));
                            break;
                        default:
                            Console.WriteLine(System.Text.Encoding.UTF8.GetString(p.data));
                            break;

                    }

                }
            }
            client.Shutdown(300);
            RakNet.RakPeerInterface.DestroyInstance(client);
            Console.Read();
        }

        private static byte GetPacketIdentifier(Packet p)
        {
            if (p == null)
                return 255;
            byte buf = p.data[0];
            if (buf == 27)
            {
                return (byte)p.data[5];
            }
            else
                return buf;
        }
    }
}
