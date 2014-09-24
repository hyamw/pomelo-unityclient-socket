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
        public const string EVENT_KICK = "kick";

		private EventManager eventManager;
		private Socket socket;
		private Protocol protocol;
		private bool disposed = false;
		private uint reqId = 1;

        private bool inited = false;
		
		public PomeloClient() {
		}

        public EventManager getEventManger() { return eventManager; }

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
            this.connect(null, null);
		}
		
		public void connect(JsonObject user){
            this.connect(user, null);
		}
		
		public void connect(Action<bool, JsonObject> handshakeCallback){
            this.connect(null, handshakeCallback);
		}
		
		public void connect(JsonObject user, Action<bool, JsonObject> handshakeCallback){
            if (!inited)
            {
                handshakeCallback(false, null);
                return;
            }

            try
            {
                protocol.start(user, (obj) => { handshakeCallback(true, obj); });
			}
            catch(Exception e)
            {
				Console.WriteLine(e.ToString());
                handshakeCallback(false, null);
			}
		}

		public void request(string route, Action<bool, JsonObject> action){
			this.request(route, new JsonObject(), action);
		}

        public void request(string route, JsonObject msg, Action<bool, JsonObject> action)
        {
            if (!inited)
            {
                action(false, null);
                return;
            }

            this.eventManager.AddCallBack(reqId, (obj) => { action(true, obj); });
			protocol.send (route, reqId, msg);

			reqId++;
		}
		
		public bool notify(string route, JsonObject msg){
            if (!inited) return false;

            protocol.send(route, msg);

            return true;
		}
		
		public void on(string eventName, Action<bool, JsonObject> action){
            if (!inited)
            {
                action(false, null);
                return;
            }

            eventManager.AddOnEvent(eventName, (obj) => { action(true, obj); });
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
                }
                catch (SocketException) { }
                finally { this.disposed = true; }

				//Call disconnect callback
				eventManager.InvokeOnEvent(EVENT_DISCONNECT, null);
			}
		}
	}
}

