using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RakNet;
namespace BigPacketTest
{
    class Program
    {
        public const short AF_INET = 2;

        static void Main(string[] args)
        {
            int BIG_PACKET_SIZE = 103296250;                                  
            bool quit;
            bool sentPacket = false;
            RakPeerInterface client, server;
            byte[] text;
            string message;
            client = server = null;
            string ip = string.Empty;

            text = new byte[BIG_PACKET_SIZE];
            quit = false;
            char ch;

            Console.WriteLine("Enter 's' to run as server, 'c' to run as client, space to run local.");
            ch = ' ';
            message = Console.ReadLine();

            ch = message.ToCharArray()[0];

            if (ch == 'c')
            {
                client = RakPeerInterface.GetInstance();
                Console.WriteLine("Working as client");
                Console.WriteLine("Enter remote IP: ");
                ip = Console.ReadLine();
                if (ip.Length == 0)
                    ip = "127.0.0.1";
            }
            else if (ch == 's')
            {
                server = RakPeerInterface.GetInstance();
                Console.WriteLine("Working as server");
            }
            else
            {
                client = RakPeerInterface.GetInstance();
                server = RakPeerInterface.GetInstance();
                ip = "127.0.0.1";
            }

            short socketFamily;
            socketFamily = AF_INET;
            if (server != null)
            {
                server.SetTimeoutTime(5000, RakNet.RakNet.UNASSIGNED_SYSTEM_ADDRESS);
                SocketDescriptor socketDescriptor = new SocketDescriptor(3000, "0");
                socketDescriptor.socketFamily = socketFamily;
                server.SetMaximumIncomingConnections(4);
                StartupResult sr = new StartupResult();
                sr = server.Startup(4, socketDescriptor, 1);
                if (sr != StartupResult.RAKNET_STARTED)
                {
                    Console.WriteLine("Error: Server failed to start: {0} ", sr.ToString());
                    return;
                }

                // server.SetPerConnectionOutgoingBandwidthLimit(50000);
                Console.WriteLine("Server started on {0}", server.GetMyBoundAddress().ToString());
            }

            if (client != null)
            {
                client.SetTimeoutTime(5000, RakNet.RakNet.UNASSIGNED_SYSTEM_ADDRESS);
                SocketDescriptor socketDescriptor = new SocketDescriptor(0, "0");
                socketDescriptor.socketFamily = socketFamily;
                client.SetMaximumIncomingConnections(4);
                StartupResult sr = new StartupResult();
                sr = client.Startup(4, socketDescriptor, 1);
                if (sr != StartupResult.RAKNET_STARTED)
                {
                    Console.WriteLine("Error: Server failed to start: " + sr.ToString());
                    return;
                }
                client.SetSplitMessageProgressInterval(10000); // Get ID_DOWNLOAD_PROGRESS notifications

                client.SetPerConnectionOutgoingBandwidthLimit(10000);
                Console.WriteLine("Client started on {0}", client.GetMyBoundAddress().ToString());
                client.Connect(ip, 3000, null, 0);
            }

            System.Threading.Thread.Sleep(500);

            Console.WriteLine("My IP addresses: ");
            RakPeerInterface rakPeer;
            if (server != null)
                rakPeer = server;
            else
                rakPeer = client;

            for (uint i = 0; i < rakPeer.GetNumberOfAddresses(); i++)
            {
                Console.WriteLine("{0}. {1}", (i + 1).ToString(), rakPeer.GetLocalIP(i).ToString());
            }

            uint start, stop = 0;

            uint nextStatTime = RakNet.RakNet.GetTimeMS() + 1000;
            Packet packet = new Packet();
            start = RakNet.RakNet.GetTimeMS();
            while (!quit)
            {
                if (server != null)
                {
                    for (packet = server.Receive(); packet != null; server.DeallocatePacket(packet), packet = server.Receive())
                    {
                        if ((DefaultMessageIDTypes)packet.data[0] == DefaultMessageIDTypes.ID_NEW_INCOMING_CONNECTION || packet.data[0] == (int)253)
                        {
                            Console.WriteLine("Starting send");
                            start = RakNet.RakNet.GetTimeMS();
                            if (BIG_PACKET_SIZE < 100000)
                            {
                                for (int i = 0; i < BIG_PACKET_SIZE; i++)
                                    text[i] = (byte)(255 - (i & 255));
                            }
                            else
                                text[0] = (byte)255;
                            DefaultMessageIDTypes idtype = (DefaultMessageIDTypes)packet.data[0];
                            if (idtype == DefaultMessageIDTypes.ID_CONNECTION_LOST)
                                Console.WriteLine("ID_CONNECTION_LOST from {0}", packet.systemAddress.ToString());
                            else if (idtype == DefaultMessageIDTypes.ID_DISCONNECTION_NOTIFICATION)
                                Console.WriteLine("ID_DISCONNECTION_NOTIFICATION from {0}", packet.systemAddress.ToString());
                            else if (idtype == DefaultMessageIDTypes.ID_NEW_INCOMING_CONNECTION)
                                Console.WriteLine("ID_NEW_INCOMING_CONNECTION from {0}", packet.systemAddress.ToString());
                            else if (idtype == DefaultMessageIDTypes.ID_CONNECTION_REQUEST_ACCEPTED)
                                Console.WriteLine("ID_CONNECTION_REQUEST_ACCEPTED from {0}", packet.systemAddress.ToString());

                            server.Send(text, BIG_PACKET_SIZE, PacketPriority.LOW_PRIORITY, PacketReliability.RELIABLE_ORDERED_WITH_ACK_RECEIPT, (char)0, packet.systemAddress, false);
                        }
                    }
                    if (Console.KeyAvailable)
                    {
                        ConsoleKeyInfo key = Console.ReadKey();
                        switch (key.Key)
                        {
                            case ConsoleKey.Spacebar:
                                Console.WriteLine("Sending medium priority message");
                                byte[] t = new byte[1];
                                t[0] = 254;
                                server.Send(t, 1, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED, (char)1, RakNet.RakNet.UNASSIGNED_SYSTEM_ADDRESS, true);
                                break;
                            case ConsoleKey.Q:
                                quit = true;
                                break;
                            default:
                                break;
                        }
                    }
                }
                if (client != null)
                {
                    packet = client.Receive();
                    while (packet != null)
                    {
                        DefaultMessageIDTypes idtype = (DefaultMessageIDTypes)packet.data[0];

                        if (idtype == DefaultMessageIDTypes.ID_DOWNLOAD_PROGRESS)
                        {
                            BitStream progressBS = new BitStream(packet.data, packet.length, false);
                            progressBS.IgnoreBits(8);
                            byte[] progress = new byte[4], total = new byte[4], partlength = new byte[4];

                            progressBS.ReadBits(progress, sizeof(uint) << 3, true);
                            progressBS.ReadBits(total, sizeof(uint) << 3, true);
                            progressBS.ReadBits(partlength, sizeof(uint) << 3, true);

                            Console.WriteLine("Progress: msgID= {0}, Progress: {1} / {2}, Partsize: {3}", packet.data[0].ToString(),
                                BitConverter.ToUInt32(progress, 0).ToString(),
                                BitConverter.ToUInt32(total, 0).ToString(),
                                BitConverter.ToUInt32(partlength, 0).ToString());

                        }
                        else if (packet.data[0] == 255)
                        {
                            if (packet.length != BIG_PACKET_SIZE)
                            {
                                Console.WriteLine("Test failed. {0} bytes (wrong number of bytes.", packet.length);
                                quit = true;
                                break;
                            }

                            if (BIG_PACKET_SIZE <= 100000)
                            {
                                for (int i = 0; i < BIG_PACKET_SIZE; i++)
                                {
                                    if (packet.data[i] != 255 - (i & 255))
                                    {
                                        Console.WriteLine("Test failed. {0} bytes (bad data).", packet.length);
                                        quit = true;
                                        break;
                                    }
                                }
                            }

                            if (quit == false)
                            {
                                Console.WriteLine("Test Succeeded. {0} bytes.", packet.length);
                                bool repeat = false;
                                if (repeat)
                                {
                                    Console.WriteLine("Rerequesting send.");
                                    byte[] ch2 = new byte[1];
                                    ch2[0] = (byte)253;
                                    client.Send(ch2, 1, PacketPriority.MEDIUM_PRIORITY, PacketReliability.RELIABLE_ORDERED, (char)1, RakNet.RakNet.UNASSIGNED_SYSTEM_ADDRESS, true);
                                    GC.Collect();
                                }
                                else
                                {
                                    quit = true;
                                    break;
                                }
                            }
                        }
                        else if ((int)packet.data[0] == 254)
                        {
                            Console.WriteLine("Got high priority message.");
                        }
                        else if ((DefaultMessageIDTypes)packet.data[0] == DefaultMessageIDTypes.ID_CONNECTION_LOST)
                            Console.WriteLine("ID_CONNECTION_LOST from {0}", packet.systemAddress.ToString());
                        else if ((DefaultMessageIDTypes)packet.data[0] == DefaultMessageIDTypes.ID_NEW_INCOMING_CONNECTION)
                            Console.WriteLine("ID_NEW_INCOMING_CONNECTION from {0}", packet.systemAddress.ToString());
                        else if ((DefaultMessageIDTypes)packet.data[0] == DefaultMessageIDTypes.ID_CONNECTION_REQUEST_ACCEPTED)
                        {
                            start = RakNet.RakNet.GetTimeMS();
                            Console.WriteLine("ID_CONNECTION_REQUEST_ACCEPTED from {0}", packet.systemAddress.ToString());
                        }
                        else if ((DefaultMessageIDTypes)packet.data[0] == DefaultMessageIDTypes.ID_CONNECTION_ATTEMPT_FAILED)
                            Console.WriteLine("ID_CONNECTION_ATTEMPT_FAILED from {0}", packet.systemAddress.ToString());
                        
                        client.DeallocatePacket(packet);
                        packet = client.Receive();
                    }
                }
                uint currenttime = RakNet.RakNet.GetTimeMS();
                if (currenttime > nextStatTime)
                {
                    nextStatTime = RakNet.RakNet.GetTimeMS() + 1000;
                    RakNetStatistics rssSender = new RakNetStatistics();
                    RakNetStatistics rssReceiver = new RakNetStatistics();
                    string StatText;

                    if (server != null)
                    {
                        ushort i;
                        ushort numSystems = 1;
                        server.GetConnectionList(null, ref numSystems);
                        if (numSystems > 0)
                        {
                            for (i = 0; i < numSystems; i++)
                            {
                                server.GetStatistics(server.GetSystemAddressFromIndex(i), rssSender);
                                RakNet.RakNet.StatisticsToString(rssSender, out StatText, 2);
                                Console.WriteLine("==== System {0} ====", (i + 1).ToString());
                                Console.WriteLine("{0}", StatText);
                            }
                        }
                    }
                    if (client != null && server == null && client.GetGUIDFromIndex(0) != RakNet.RakNet.UNASSIGNED_RAKNET_GUID)
                    {
                        client.GetStatistics(client.GetSystemAddressFromIndex(0), rssReceiver);
                        RakNet.RakNet.StatisticsToString(rssReceiver, out StatText, 2);
                        Console.WriteLine("{0}", StatText);
                    }
                }
                System.Threading.Thread.Sleep(100);
            }
            string StatTextEnd = "";
            stop = RakNet.RakNet.GetTimeMS();
            double seconds = (double)(stop - start) / 1000.0;
            if (server != null)
            {
                RakNetStatistics rssSender2 = server.GetStatistics(server.GetSystemAddressFromIndex(0));
                RakNet.RakNet.StatisticsToString(rssSender2, out StatTextEnd, 2);
                Console.WriteLine("{0}", StatTextEnd);
            }
            Console.WriteLine("{0} bytes per second ({1} seconds). Press enter to quit", (int)((double)(BIG_PACKET_SIZE) / seconds), seconds);
            RakPeerInterface.DestroyInstance(server);
            RakPeerInterface.DestroyInstance(client);
            Console.Read();

        }
    }
}
