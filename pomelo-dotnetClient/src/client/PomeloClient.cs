using System.Collections;
using SimpleJson;

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;

namespace Pomelo.DotNetClient
{
	public class PomeloClient : IDisposable
	{
		public const string EVENT_DISCONNECT = "disconnect";

		private EventManager eventManager;
		private Socket socket;
		private Protocol protocol;
		private bool disposed = false;
		private uint reqId = 1;

        private bool inited = false;
		
		public PomeloClient() {
		}

        public void init(string host, int port, Action<bool> onConnected) {
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ie = new IPEndPoint(IPAddress.Parse(host), port);

            socket.BeginConnect(ie, new AsyncCallback((result) =>
            {
                try
                {
                    this.socket.EndConnect(result);

                    this.protocol = new Protocol(this, this.socket);
                    this.eventManager = new EventManager();

                    inited = true;

                    if (onConnected != null) onConnected(true);
                }
                catch (SocketException e)
                {
                    Console.WriteLine(String.Format("unable to connect to server: {0}", e.ToString()));
                    this.socket = null;

                    if (onConnected != null) onConnected(false);
                }
            }), this.socket);
        }

		public void connect(){
            if (!inited) return;
			protocol.start(null, null);
		}
		
		public void connect(JsonObject user){
            if (!inited) return;
            protocol.start(user, null);
		}
		
		public void connect(Action<JsonObject> handshakeCallback){
            if (!inited) return;
            protocol.start(null, handshakeCallback);
		}
		
		public bool connect(JsonObject user, Action<JsonObject> handshakeCallback){
            if (!inited) return false;

            try
            {
				protocol.start(user, handshakeCallback);
				return true;
			}catch(Exception e){
				Console.WriteLine(e.ToString());
				return false;
			}
		}

		public void request(string route, Action<JsonObject> action){
			this.request(route, new JsonObject(), action);
		}
		
		public void request(string route, JsonObject msg, Action<JsonObject> action){
            if (!inited) return;
            this.eventManager.AddCallBack(reqId, action);
			protocol.send (route, reqId, msg);

			reqId++;
		}
		
		public void notify(string route, JsonObject msg){
            if (!inited) return;
            protocol.send(route, msg);
		}
		
		public void on(string eventName, Action<JsonObject> action){
            if (!inited) return;
            eventManager.AddOnEvent(eventName, action);
		}

		internal void processMessage(Message msg){
			if(msg.type == MessageType.MSG_RESPONSE){
				eventManager.InvokeCallBack(msg.id, msg.data);
			}else if(msg.type == MessageType.MSG_PUSH){
				eventManager.InvokeOnEvent(msg.route, msg.data);
			}
		}

		public void disconnect(){
            Dispose();
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		// The bulk of the clean-up code 
		protected virtual void Dispose(bool disposing) {
            if (!inited) return;
            if (this.disposed) return;

			if (disposing) {
				// free managed resources
				this.protocol.close();

                try
                {
                    this.socket.Shutdown(SocketShutdown.Both);
                    this.socket.Close();
                    this.disposed = true;
                }
                catch (SocketException) { }

				//Call disconnect callback
				eventManager.InvokeOnEvent(EVENT_DISCONNECT, null);
			}
		}
	}
}

