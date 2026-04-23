using FluentValidation;
using SMSMarica.Core.Rastreamento.Dtos;

namespace SMSMarica.Core.Rastreamento.Validators;

public sealed class RegistrarPontoGpsValidator : AbstractValidator<RegistrarPontoGpsRequest>
{
    public RegistrarPontoGpsValidator()
    {
        RuleFor(p => p.MotoristaId).NotEmpty();
        RuleFor(p => p.Latitude).InclusiveBetween(-90, 90);
        RuleFor(p => p.Longitude).InclusiveBetween(-180, 180);
        RuleFor(p => p.CapturadoEm).NotEmpty().LessThanOrEqualTo(_ => DateTime.UtcNow.AddMinutes(5));
    }
}

public sealed class CadastrarGeofenceValidator : AbstractValidator<CadastrarGeofenceRequest>
{
    public CadastrarGeofenceValidator()
    {
        RuleFor(g => g.Tipo).IsInEnum();
        RuleFor(g => g.ReferenciaId).NotEmpty();
        RuleFor(g => g.Latitude).InclusiveBetween(-90, 90);
        RuleFor(g => g.Longitude).InclusiveBetween(-180, 180);
        RuleFor(g => g.RaioMetros).InclusiveBetween(10, 5000);
    }
}

public sealed class AtualizarGeofenceValidator : AbstractValidator<AtualizarGeofenceRequest>
{
    public AtualizarGeofenceValidator()
    {
        RuleFor(g => g.Latitude).InclusiveBetween(-90, 90);
        RuleFor(g => g.Longitude).InclusiveBetween(-180, 180);
        RuleFor(g => g.RaioMetros).InclusiveBetween(10, 5000);
    }
}
