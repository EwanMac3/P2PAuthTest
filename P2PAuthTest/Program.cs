using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace P2PAuthTest {
class Program {
	static void Main(string[] args) {
		Console.WriteLine("[s]erver or [c]lient?");
		string input = Console.ReadLine();
		if (input == "s") {
			startServer();
		}
		else {
			startClient();
		}
	}

	static void startServer() {
		StringCrypt encry = new StringCrypt();
		int pubKey = 0;
		int goodCount = 0;
		int badCount = 0;
		//match key / public identifier, this should be on a per-match basis
		Dictionary<string, string> idToMatchKey = new Dictionary<string, string>();
		Dictionary<string, string> matchKeyToId = new Dictionary<string, string>();
		Server serv = new Server(6372);
		while (true) {
			while (!serv.incomingMsgs.IsEmpty) {
				NewMessageData m;
				if (!serv.incomingMsgs.TryDequeue(out m)) {
					continue;
				}

				//Console.WriteLine("Raw in: "+m.message);
				//Log a validated / failed ID check based on given encrypted string and public key it claims to belong to
				if (m.message.StartsWith("checkKey")) {
					string[] parts = m.message.Split(":::");
					string decPriv = encry.Decrypt(matchKeyToId[parts[1]], parts[2]);
					if (matchKeyToId.ContainsKey(decPriv)) {
						Console.WriteLine(decPriv + " validated with private key");
						goodCount++;
					}
					else {
						Console.WriteLine("Someone failed to authenticate");
						badCount++;
					}
				}

				//report status once all keys have been checked
				if (goodCount + badCount == 6) {
					if (goodCount == 6) {
						Console.WriteLine("All players verified with each other! Match start!");
						serv.sendAll("AllKeyGood");
					}
					else {
						Console.WriteLine("Some players could not be verified!");
						serv.sendAll("KeyCheckFail");
					}
				}
				//assign a public key for a given identifier
				else if (m.message.StartsWith("assignPubKey")) {
					string[] parts = m.message.Split(":::");
					idToMatchKey.Add(parts[1], pubKey + "isMyPublic");
					matchKeyToId.Add(pubKey + "isMyPublic", parts[1]);
					serv.sendMsg(m.senderId, "pubKey:::" + pubKey + "isMyPublic");
					pubKey++;
				}
			}
		}
	}


	static void startClient() {
		bool askedForPubKey = false; //did we get a public key from the server?
		bool sendEncryKey = false; // used for testing
		bool sentPlayer1Key = false; //did we send the other player 1 our enc. key?
		bool sentPlayer2Key = false; //did we send the other player 2 our enc. key?
		bool askedForVerify = false; //did we already ask gamblitz master server to check what we were given?
		StringCrypt encry = new StringCrypt();
		string pubKey = null; //public key from server
		string encryptID = null; //encrypted string of our pubkey based on id
		Console.WriteLine("Enter identifier string");
		string identifierEpic = Console.ReadLine();

		//connect to main gamblitz server
		Client gamClient = new Client("localhost", 6372); //curl or some shit

		//host authentication p2p server
		Console.WriteLine("Enter host port");
		int port = Convert.ToInt32(Console.ReadLine());
		Server playerServer = new Server(port);

		//connect to the other players' p2p auth servers
		Console.WriteLine("Enter 1st other player IP address");
		string playerIP = "localhost"; //Console.ReadLine();
		Console.WriteLine("Enter 1st other player port");
		port = Convert.ToInt32(Console.ReadLine());
		Client playerClient1 = new Client(playerIP, port);

		Console.WriteLine("Enter 2nd other player IP address");
		//playerIP = Console.ReadLine();
		Console.WriteLine("Enter 2nd other player port");
		port = Convert.ToInt32(Console.ReadLine());
		Client playerClient2 = new Client(playerIP, port);

		//keystring we get from other players
		string recKey1 = null;
		string recKey2 = null;

		while (true) {
			//This section handles communication with the "master" gamblitz server
			while (!gamClient.incomingMsgs.IsEmpty) {
				string message;
				if (!gamClient.incomingMsgs.TryDequeue(out message)) {
					continue;
				}

				//Store public key assigned by server
				if (message.StartsWith("pubKey:::")) {
					pubKey = message.Split(":::")[1];
					encryptID = encry.Encrypt(identifierEpic, pubKey);
				}

				if (message == "AllKeyGood") {
					Console.WriteLine("All players verified with each other! Match start!");
				}

				if (message == "KeyCheckFail") {
					Console.WriteLine("Some players could not be verified!");
				}
			}

			//Ask for a public key based on saved identifier (gbident.epic)
			if (!askedForPubKey) {
				gamClient.sendMsg("assignPubKey:::" + identifierEpic);
				askedForPubKey = true;
			}

			/*	//Send our encrypted public key to server w/ our ID as key (testing)
				if (!sendEncryKey && pubKey != null) {
					string encryptID = encry.Encrypt("PrivateIDAAA", pubKey);
					gamClient.sendMsg("checkKey:::" + pubKey + ":::" + encryptID);
					sendEncryKey = true;
				}
			*/


			//This section handles 'p2p' communication with the other players in the LAN lobby
			while (!playerServer.incomingMsgs.IsEmpty) {
				NewMessageData message;
				if (!playerServer.incomingMsgs.TryDequeue(out message)) {
					continue;
				}

				//store the encrypted keys received from other players
				if (recKey1 != null)
					recKey2 = message.message;
				else
					recKey1 = message.message;
			}

			//send 1st other player our encrypted ID
			if (!sentPlayer1Key && encryptID != null) {
				playerClient1.sendMsg(pubKey + ":::" + encryptID);
				sentPlayer1Key = true;
			}

			//send 2nd other player our encrypted ID
			if (!sentPlayer2Key && encryptID != null) {
				playerClient2.sendMsg(pubKey + ":::" + encryptID);
				sentPlayer2Key = true;
			}

			//if our communication is done, ask GBZ server to check everything
			if (recKey1 != null && recKey2 != null && !askedForVerify) {
				askedForVerify = true;
				gamClient.sendMsg("checkKey:::" + recKey1);
				gamClient.sendMsg("checkKey:::" + recKey2);
			}
		}
	}
}
}