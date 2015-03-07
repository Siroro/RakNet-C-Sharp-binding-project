using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RakNet;
namespace ChatExampleServer
{
    class Program
    {
        public const short AF_INET = 2;
        public const short AF_INET6 = 23;
        public const int MAXIMUM_NUMBER_OF_INTERNAL_IDS = 10;
        static void Main(string[] args)
        {
            RakNetStatistics rss = new RakNetStatistics();
            RakPeerInterface server = RakPeerInterface.GetInstance();
            server.SetIncomingPassword("Rumpelstiltskin", "Rumpelstiltskin".Length);
            server.SetTimeoutTime(30000, RakNet.RakNet.UNASSIGNED_SYSTEM_ADDRESS);

            Packet p = new Packet();
            RakNet.SystemAddress clientID = RakNet.RakNet.UNASSIGNED_SYSTEM_ADDRESS;
            byte packetIdentifier;
            bool isServer = true;

            string serverPort;

            Console.WriteLine("Enter the client port to listen on");
            serverPort = Console.ReadLine();
            if (serverPort.Length == 0)
                serverPort = "1234";

            Console.WriteLine("Starting server");
            RakNet.SocketDescriptor socketDescriptors = new SocketDescriptor(Convert.ToUInt16(serverPort), "0");
            socketDescriptors.port = Convert.ToUInt16(serverPort);
            socketDescriptors.socketFamily = AF_INET;


            StartupResult sar = server.Startup(4, socketDescriptors, 1);
            if (sar != StartupResult.RAKNET_STARTED)
                Console.WriteLine("Error starting server");

            server.SetMaximumIncomingConnections(4);
            System.Threading.Thread.Sleep(1000);
            server.SetOccasionalPing(true);
            server.SetUnreliableTimeout(1000);


	        for (int i=0; i < server.GetNumberOfAddresses(); i++)
	        {
                SystemAddress sa = server.GetInternalID(RakNet.RakNet.UNASSIGNED_SYSTEM_ADDRESS, i);
                Console.WriteLine((i+1).ToString() + ". " + sa.ToString() + "(LAN = " + sa.IsLANAddress() + ")");
	        }
            Console.WriteLine("My GUID is " + server.GetGuidFromSystemAddress(RakNet.RakNet.UNASSIGNED_SYSTEM_ADDRESS).ToString());
            Console.WriteLine("'quit' to quit. 'stat' to show stats. 'ping' to ping.\n'pingip' to ping an ip address\n'ban' to ban an IP from connecting.\n'kick to kick the first connected player.\nType to talk.");
            string message;

            while (true)
            {
                System.Threading.Thread.Sleep(30);
                if (Console.KeyAvailable)
                {
                    message = Console.ReadLine();

                    if (message == "quit")
                    {
                        Console.WriteLine("Quitting");
                        break;
                    }

                    if (message == "kick")
                    {
                        server.CloseConnection(clientID,true,0);
                        continue;
                    }

                    if (message == "stat")
                    {
                        rss = server.GetStatistics(server.GetSystemAddressFromIndex(0));
                        RakNet.RakNet.StatisticsToString(rss, out message, 2);
                        Console.WriteLine(message);
                        continue;
                    }
                    if (message == "ping")
                    {
                        server.Ping(clientID);
                        continue;
                    }
                    if (message == "list")
                    {
                        SystemAddress[] systems = new SystemAddress[10];
                        ushort numCons = 10;
                        server.GetConnectionList(out systems, ref numCons);
                        for (int i=0; i < numCons; i++)
                        {
                            Console.WriteLine((i+1).ToString() + ". " + systems[i].ToString(true));
                        }
                        continue;
                    }

                    if (message == "ban")
                    {
                        Console.WriteLine("'Enter IP to ban.  You can use * as a wildcard");
                        message = Console.ReadLine();
                        server.AddToBanList(message);
                        Console.WriteLine("IP " + message + " added to ban list.");
                        continue;
                    }

                    string message2;
                    message2 = "Server: " + message;
                    server.Send(message2, message2.Length + 1, PacketPriority.HIGH_PRIORITY, PacketReliability.RELIABLE_ORDERED, (char)0, RakNet.RakNet.UNASSIGNED_SYSTEM_ADDRESS, true);
                }

                for (p = server.Receive(); p != null; server.DeallocatePacket(p), p = server.Receive())
                {
                    packetIdentifier = GetPacketIdentifier(p);
                    switch ((DefaultMessageIDTypes)packetIdentifier)
                    {
                        case DefaultMessageIDTypes.ID_DISCONNECTION_NOTIFICATION:
                            Console.WriteLine("ID_DISCONNECTION_NOTIFICATION from " + p.systemAddress.ToString(true));
                            break;
                        case DefaultMessageIDTypes.ID_NEW_INCOMING_CONNECTION:
                            Console.WriteLine("ID_NEW_INCOMING_CONNECTION from " + p.systemAddress.ToString(true) + "with GUID " + p.guid.ToString());
                            clientID = p.systemAddress;
                            Console.WriteLine("Remote internal IDs: ");
                            for (int index = 0; index < MAXIMUM_NUMBER_OF_INTERNAL_IDS; index++)
                            {
                                SystemAddress internalId = server.GetInternalID(p.systemAddress, index);
                                if (internalId != RakNet.RakNet.UNASSIGNED_SYSTEM_ADDRESS)
                                {
                                    Console.WriteLine((index + 1).ToString() + ". " + internalId.ToString(true));
                                }
                            }
                            break;
                        case DefaultMessageIDTypes.ID_INCOMPATIBLE_PROTOCOL_VERSION:
                            Console.WriteLine("ID_INCOMPATIBLE_PROTOCOL_VERSION");
                            break;
                        case DefaultMessageIDTypes.ID_CONNECTED_PING:
                        case DefaultMessageIDTypes.ID_UNCONNECTED_PING:
                            Console.WriteLine("Ping from " + p.systemAddress.ToString(true));
                            break;
                        case DefaultMessageIDTypes.ID_CONNECTION_LOST:
                            Console.WriteLine("ID_CONNECTION_LOST from " + p.systemAddress.ToString(true));
                            break;
                        default:
                            Console.WriteLine(System.Text.Encoding.UTF8.GetString(p.data));
                            message = System.Text.Encoding.UTF8.GetString(p.data);
                            server.Send(message, message.Length + 1, PacketPriority.HIGH_PRIORITY, PacketReliability.RELIABLE_ORDERED, (char)0, RakNet.RakNet.UNASSIGNED_SYSTEM_ADDRESS, true);
                            break;
                    }
                }
            }
            server.Shutdown(300);
            RakNet.RakPeerInterface.DestroyInstance(server);
            Console.Read();
        }

        private static byte GetPacketIdentifier(Packet p)
        {
            if (p == null)
                return 255;
            byte buf = p.data[0];
            if (buf == (char)DefaultMessageIDTypes.ID_TIMESTAMP)
            {
                return (byte)p.data[5];
            }
            else
                return buf;
        }
    }
}
