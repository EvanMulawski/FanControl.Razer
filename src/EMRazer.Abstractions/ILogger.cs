﻿namespace EMRazer;

public interface ILogger
{
    void Log(string message);
    void Normal(string deviceName, string message);
    void Error(string deviceName, string message);
    void Debug(string deviceName, string message);
}