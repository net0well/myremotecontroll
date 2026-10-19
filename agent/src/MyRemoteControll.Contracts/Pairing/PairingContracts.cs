using MyRemoteControll.Contracts.Devices;

namespace MyRemoteControll.Contracts.Pairing;

/// <summary>Corpo de <c>POST /api/v1/pairing/complete</c>; o código vem do QR exibido na bandeja.</summary>
public sealed record CompletePairingRequest(string Code, string DeviceName, DevicePlatform Platform);

/// <summary>O token é devolvido uma única vez; o agente guarda apenas o HMAC dele.</summary>
public sealed record CompletePairingResponse(Guid DeviceId, string Token, DeviceRole Role, string PcName);

public sealed record RotateTokenResponse(string Token, DateTimeOffset PreviousValidUntil);
