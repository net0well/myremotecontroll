namespace MyRemoteControll.Contracts.Devices;

public enum DevicePlatform
{
    Android,
    Ios,
}

/// <summary>Papéis da seção 9 do documento base (ADR-0009).</summary>
public enum DeviceRole
{
    ReadOnly,
    Operator,
    Administrator,
}
