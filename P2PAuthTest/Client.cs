using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace P2PAuthTest {
public class Client {
	private TcpClient tcpClient;
	public ConcurrentQueue<string> incomingMsgs;
	private string extraDelim = "{E6F77CED-C67F-4C79-AD71-4503F3CE8685}";

	public Client(string ip, int port) {
		incomingMsgs = new ConcurrentQueue<string>();
		tcpClient = new TcpClient();
		tcpClient.Connect(ip, port);
		new Thread(() => processNewMsgs()).Start();
	}

	public void sendMsg(string message) {
		if (tcpClient.Connected)
			tcpClient.GetStream().Write(Encoding.UTF8.GetBytes(message + extraDelim));
	}

	void processNewMsgs() {
		while (true) {
			Thread.Sleep(50);
			if (tcpClient.Connected) {
				byte[] receiveBuffer = new byte[2048];
				int bytesReceived = tcpClient.GetStream().Read(receiveBuffer);
				string data = Encoding.UTF8.GetString(receiveBuffer.AsSpan(0, bytesReceived));
				if (data.Length > 0) {
					string[] multiIn = data.Split(extraDelim);
					for (int i = 0; i < multiIn.Length; i++) {
						if (multiIn[i].Trim().Length > 0)
							incomingMsgs.Enqueue(multiIn[i]);
					}
				}
			}

			GC.Collect();
		}
	}
}
}