internal interface IRazerPwmFanController
{
    bool Connect();
    void Disconnect();
    int GetChannelSpeed(int channel);
    void SetChannelMode(int channel, byte mode);
    void SetChannelPower(int channel, int power, byte registerToSet);
}