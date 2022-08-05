using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace P2PAuthTest {
public class Server {
	TcpListener server;
	ConcurrentDictionary<int, TcpClient> clients;
	public ConcurrentQueue<NewMessageData> incomingMsgs;
	private int cid = 0;
	private string extraDelim = "{E6F77CED-C67F-4C79-AD71-4503F3CE8685}";

	public Server(int port) {
		clients = new ConcurrentDictionary<int, TcpClient>();
		incomingMsgs = new ConcurrentQueue<NewMessageData>();
		IPAddress localHost = IPAddress.Parse("127.0.0.1");
		server = new TcpListener(localHost, port);
		server.Start();
		new Thread(addNewClients).Start();
	}

	void addNewClients() {
		while (true) {
			TcpClient newCli = server.AcceptTcpClient();
			cid++;
			new Thread(() => processNewMsgs(newCli, cid)).Start();
			while (!clients.TryAdd(cid, newCli)) {
				//add this shit dude
			}
		}
	}

	public void sendMsg(int id, string message) {
		TcpClient cli = clients[id];
		if (cli.Connected) {
			cli.GetStream().Write(Encoding.UTF8.GetBytes(message + extraDelim));
		}
	}

	public void sendAll(string message) {
		for (int i = 1; i <= clients.Count; i++) {
			sendMsg(i, message);
		}
	}


	void processNewMsgs(TcpClient client, int id) {
		while (true) {
			Thread.Sleep(50);
			if (client.Connected) {
				byte[] receiveBuffer = new byte[2048];
				int bytesReceived = client.GetStream().Read(receiveBuffer);
				string data = Encoding.UTF8.GetString(receiveBuffer.AsSpan(0, bytesReceived));
				if (data.Length > 0) {
					string[] multiIn = data.Split(extraDelim);
					for (int i = 0; i < multiIn.Length; i++) {
						if (multiIn[i].Trim().Length > 0) {
							NewMessageData m = new NewMessageData {
								senderId = id,
								message = multiIn[i],
								senderIp = ((IPEndPoint)(client.Client.RemoteEndPoint)).Address.ToString(),
								timeStamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
							};
							incomingMsgs.Enqueue(m);
						}
					}
				}
			}

			GC.Collect();
		}
	}
}

public class NewMessageData {
	public int senderId;
	public string senderIp;
	public long timeStamp;
	public string message;
}
}