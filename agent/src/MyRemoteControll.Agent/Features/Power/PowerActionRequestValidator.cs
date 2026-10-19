using FluentValidation;
using MyRemoteControll.Contracts.Power;

namespace MyRemoteControll.Agent.Features.Power;

internal sealed class PowerActionRequestValidator : AbstractValidator<PowerActionRequest>
{
    /// <summary>Atraso máximo: 24 h (spec 005).</summary>
    public const int MaxDelaySeconds = 24 * 60 * 60;

    public PowerActionRequestValidator()
    {
        RuleFor(r => r.Action).IsInEnum().WithMessage("Ação de energia desconhecida.");
        RuleFor(r => r.DelaySeconds)
            .InclusiveBetween(0, MaxDelaySeconds)
            .WithMessage("O atraso deve ficar entre 0 e 24 horas.");
    }
}
