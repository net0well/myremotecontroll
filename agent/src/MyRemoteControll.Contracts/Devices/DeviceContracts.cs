namespace MyRemoteControll.Contracts.Devices;

public sealed record DeviceDto(
    Guid Id,
    string Name,
    DevicePlatform Platform,
    DeviceRole Role,
    IReadOnlyList<string> ExtraPermissions,
    IReadOnlyList<string> EffectivePermissions,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSeenAt,
    bool Connected,
    bool Revoked);

/// <summary>Novo papel e permissões extras; <see cref="Name"/> nulo mantém o nome atual.</summary>
public sealed record UpdateDevicePermissionsRequest(DeviceRole Role, IReadOnlyList<string> ExtraPermissions, string? Name = null);

/// <summary>Enviado ao próprio aparelho quando o papel ou as permissões dele mudam (vale na próxima chamada).</summary>
public sealed record DevicePermissionsChangedDto(Guid DeviceId, DeviceRole Role, IReadOnlyList<string> EffectivePermissions);
