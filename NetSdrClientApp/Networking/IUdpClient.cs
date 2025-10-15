﻿
public interface IUdpClient
{
    event EventHandler<byte[]>? MessageReceived;

    Task StartListeningAsync();

    void Exit();
}