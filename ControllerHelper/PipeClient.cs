﻿using ControllerService;
using NamedPipeWrapper;
using Serilog.Core;
using SharpDX.XInput;
using System;
using System.Collections.Concurrent;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ControllerHelper
{
    public class PipeClient
    {
        private readonly NamedPipeClient<PipeMessage> client;
        private readonly ControllerHelper helper;
        private readonly Logger logger;

        private ConcurrentQueue<PipeMessage> m_queue;
        private Timer m_timer;

        public bool connected;

        public PipeClient(string pipeName, ControllerHelper helper, Logger logger)
        {
            this.helper = helper;
            this.logger = logger;

            m_queue = new ConcurrentQueue<PipeMessage>();

            // monitors processes and settings
            m_timer = new Timer(1000) { Enabled = false, AutoReset = true };
            m_timer.Elapsed += SendMessageQueue;

            client = new NamedPipeClient<PipeMessage>(pipeName);
            client.AutoReconnect = true;

            client.Disconnected += OnClientDisconnected;
            client.ServerMessage += OnServerMessage;
            client.Error += OnError;
        }

        private void OnClientDisconnected(NamedPipeConnection<PipeMessage, PipeMessage> connection)
        {
            connected = false;
            helper.UpdateStatus(false);
        }

        public void Start()
        {
            if (client == null)
                return;

            client.Start();
        }

        public void Stop()
        {
            if (client == null)
                return;

            client.Stop();
        }

        private void OnServerMessage(NamedPipeConnection<PipeMessage, PipeMessage> connection, PipeMessage message)
        {
            switch (message.Code)
            {
                case PipeCode.SERVER_CONNECTED:
                    connected = true;
                    helper.UpdateStatus(true);
                    helper.UpdateScreen();
                    break;

                case PipeCode.SERVER_TOAST:
                    Utils.SendToast(message.args["title"], message.args["content"]);
                    break;

                case PipeCode.SERVER_CONTROLLER:
                    helper.UpdateController(message.args);
                    break;

                case PipeCode.SERVER_SETTINGS:
                    helper.UpdateSettings(message.args);
                    break;
            }
        }

        private void OnError(Exception exception)
        {
            // logger.LogError("PipClient failed. {0}", exception.Message);
        }

        public void SendMessage(PipeMessage message)
        {
            if (!connected)
            {
                m_queue.Enqueue(message);
                m_timer.Start();
                return;
            }

            client.PushMessage(message);
        }

        private void SendMessageQueue(object sender, ElapsedEventArgs e)
        {
            if (!connected)
                return;

            foreach (PipeMessage m in m_queue)
                client.PushMessage(m);

            m_queue.Clear();
            m_timer.Stop();
        }
    }
}
