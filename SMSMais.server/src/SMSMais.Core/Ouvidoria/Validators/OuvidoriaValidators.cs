using FluentValidation;
using SMSMais.Core.Ouvidoria.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Ouvidoria.Validators;

// Validação de forma dos requests da ouvidoria. As regras que dependem de estado (identificação ×
// tipo, transições, unicidade) ficam no service e saem como ValidacaoException/ConflitoException.

internal static class OuvidoriaRegras
{
    public const int TeorMin = 10;
    public const int TeorMax = 10_000;
    public const int ResumoMax = 200;

    /// <summary>CPF: 11 dígitos quando informado (pontuação é ignorada).</summary>
    public static bool CpfValido(string? v) => string.IsNullOrWhiteSpace(v) || SoDigitos(v).Length == 11;

    /// <summary>Telefone: 10 a 13 dígitos quando informado (DDD+número, com ou sem 55).</summary>
    public static bool TelefoneValido(string? v) => string.IsNullOrWhiteSpace(v) || SoDigitos(v).Length is >= 10 and <= 13;

    public static bool CnsValido(string? v) => string.IsNullOrWhiteSpace(v) || SoDigitos(v).Length == 15;

    private static string SoDigitos(string s) => new([.. s.Where(char.IsDigit)]);
}

public sealed class ManifestanteValidator : AbstractValidator<ManifestanteDto>
{
    public ManifestanteValidator()
    {
        RuleFor(m => m.Nome).MaximumLength(200);
        RuleFor(m => m.Cpf).Must(OuvidoriaRegras.CpfValido).WithMessage("CPF deve ter 11 dígitos.");
        RuleFor(m => m.Telefone).Must(OuvidoriaRegras.TelefoneValido).WithMessage("Telefone deve ter de 10 a 13 dígitos.");
        RuleFor(m => m.Email).MaximumLength(200).EmailAddress().When(m => !string.IsNullOrWhiteSpace(m.Email));
    }
}

public sealed class ReferidoValidator : AbstractValidator<ReferidoDto>
{
    public ReferidoValidator()
    {
        RuleFor(r => r.Nome).MaximumLength(200);
        RuleFor(r => r.Cpf).Must(OuvidoriaRegras.CpfValido).WithMessage("CPF deve ter 11 dígitos.");
        RuleFor(r => r.Cns).Must(OuvidoriaRegras.CnsValido).WithMessage("CNS deve ter 15 dígitos.");
    }
}

public sealed class AnexoRefValidator : AbstractValidator<AnexoRef>
{
    public AnexoRefValidator()
    {
        RuleFor(a => a.MidiaId).NotEmpty();
        RuleFor(a => a.NomeArquivo).NotEmpty().MaximumLength(300);
    }
}

public sealed class RegistrarManifestacaoValidator : AbstractValidator<RegistrarManifestacaoRequest>
{
    public RegistrarManifestacaoValidator()
    {
        RuleFor(r => r.Tipo).IsInEnum();
        RuleFor(r => r.Identificacao).IsInEnum();
        RuleFor(r => r.Canal).IsInEnum();
        RuleFor(r => r.Origem).IsInEnum();
        RuleFor(r => r.Teor).NotEmpty().MinimumLength(OuvidoriaRegras.TeorMin).MaximumLength(OuvidoriaRegras.TeorMax);
        RuleFor(r => r.Resumo).MaximumLength(OuvidoriaRegras.ResumoMax);
        RuleFor(r => r.LocalFato).MaximumLength(200);
        RuleFor(r => r.EnvolvidoDescricao).MaximumLength(300);
        RuleFor(r => r.ProtocoloExterno).MaximumLength(60);
        RuleFor(r => r.SistemaExterno).MaximumLength(40);
        RuleFor(r => r.Manifestante!).SetValidator(new ManifestanteValidator()).When(r => r.Manifestante is not null);
        RuleFor(r => r.Referido!).SetValidator(new ReferidoValidator()).When(r => r.Referido is not null);
        RuleForEach(r => r.Anexos).SetValidator(new AnexoRefValidator());
    }
}

public sealed class RegistrarManifestacaoPublicaValidator : AbstractValidator<RegistrarManifestacaoPublicaRequest>
{
    public RegistrarManifestacaoPublicaValidator()
    {
        RuleFor(r => r.Tipo).IsInEnum();
        RuleFor(r => r.Identificacao).IsInEnum();
        RuleFor(r => r.Teor).NotEmpty().MinimumLength(OuvidoriaRegras.TeorMin).MaximumLength(OuvidoriaRegras.TeorMax);
        RuleFor(r => r.LocalFato).MaximumLength(200);
        RuleFor(r => r.EnvolvidoDescricao).MaximumLength(300);
        RuleFor(r => r.Manifestante!).SetValidator(new ManifestanteValidator()).When(r => r.Manifestante is not null);
        RuleFor(r => r.Referido!).SetValidator(new ReferidoValidator()).When(r => r.Referido is not null);
    }
}

public sealed class TriarValidator : AbstractValidator<TriarRequest>
{
    public TriarValidator()
    {
        When(r => r.Tipo.HasValue, () => RuleFor(r => r.Tipo!.Value).IsInEnum());
        When(r => r.Prioridade.HasValue, () => RuleFor(r => r.Prioridade!.Value).IsInEnum());
        RuleFor(r => r.Resumo).MaximumLength(OuvidoriaRegras.ResumoMax);
    }
}

public sealed class EncaminharValidator : AbstractValidator<EncaminharRequest>
{
    public EncaminharValidator()
    {
        RuleFor(r => r.PontoRespostaId).NotEmpty();
        RuleFor(r => r.PrazoDias).InclusiveBetween(1, 365).When(r => r.PrazoDias.HasValue);
        RuleFor(r => r.Texto).MaximumLength(OuvidoriaRegras.TeorMax);
        RuleFor(r => r.TeorPseudonimizado).MaximumLength(OuvidoriaRegras.TeorMax);
    }
}

/// <summary>
/// <c>TextoRequest</c> serve a várias ações com mínimos diferentes; aqui vale o piso comum (≥ 5). A
/// prorrogação (≥ 20) e a revelação de identidade (≥ 15) reforçam o mínimo no service.
/// </summary>
public sealed class TextoValidator : AbstractValidator<TextoRequest>
{
    public TextoValidator()
    {
        RuleFor(r => r.Texto).NotEmpty().MinimumLength(5).MaximumLength(OuvidoriaRegras.TeorMax);
    }
}

public sealed class TextoComAnexosValidator : AbstractValidator<TextoComAnexosRequest>
{
    public TextoComAnexosValidator()
    {
        RuleFor(r => r.Texto).NotEmpty().MinimumLength(5).MaximumLength(OuvidoriaRegras.TeorMax);
        RuleForEach(r => r.Anexos).SetValidator(new AnexoRefValidator());
    }
}

public sealed class ResponderCidadaoValidator : AbstractValidator<ResponderCidadaoRequest>
{
    public ResponderCidadaoValidator()
    {
        RuleFor(r => r.Texto).NotEmpty().MaximumLength(OuvidoriaRegras.TeorMax);
        When(r => r.Conclusiva, () =>
        {
            RuleFor(r => r.Texto).MinimumLength(20).WithMessage("A resposta conclusiva precisa de pelo menos 20 caracteres.");
            RuleFor(r => r.Resolutividade).NotNull().WithMessage("Resposta conclusiva exige a resolutividade.");
            RuleFor(r => r.SituacaoFinal).NotNull().WithMessage("Resposta conclusiva exige a situação final.");
        });
        When(r => r.Resolutividade.HasValue, () => RuleFor(r => r.Resolutividade!.Value).IsInEnum());
        When(r => r.SituacaoFinal.HasValue, () => RuleFor(r => r.SituacaoFinal!.Value).IsInEnum());
        When(r => !r.Conclusiva, () => RuleFor(r => r.Texto).MinimumLength(5));
        When(r => r.MotivoNaoAtendimento.HasValue, () => RuleFor(r => r.MotivoNaoAtendimento!.Value).IsInEnum());
    }
}

public sealed class ArquivarValidator : AbstractValidator<ArquivarRequest>
{
    public ArquivarValidator()
    {
        RuleFor(r => r.Motivo).IsInEnum();
        RuleFor(r => r.Texto).MaximumLength(OuvidoriaRegras.TeorMax);
        RuleFor(r => r.Texto).NotEmpty().WithMessage("Arquivamento por duplicidade exige o protocolo da manifestação original.")
            .When(r => r.Motivo == OuvidoriaMotivoArquivamento.Duplicidade);
    }
}

public sealed class EncaminharExternoValidator : AbstractValidator<EncaminharExternoRequest>
{
    public EncaminharExternoValidator()
    {
        RuleFor(r => r.SistemaExterno).NotEmpty().MaximumLength(40);
        RuleFor(r => r.ProtocoloExterno).MaximumLength(60);
        RuleFor(r => r.Texto).NotEmpty().MinimumLength(5).MaximumLength(OuvidoriaRegras.TeorMax);
    }
}

public sealed class SalvarPontoRespostaValidator : AbstractValidator<SalvarPontoRespostaRequest>
{
    public SalvarPontoRespostaValidator()
    {
        RuleFor(r => r.Nome).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Tipo).IsInEnum();
        RuleFor(r => r.UnidadeId).NotNull().WithMessage("Ponto do tipo Unidade exige a unidade.")
            .When(r => r.Tipo == OuvidoriaTipoPontoResposta.Unidade);
        RuleFor(r => r.PrazoDias).InclusiveBetween(1, 365).When(r => r.PrazoDias.HasValue);
        RuleForEach(r => r.Membros).ChildRules(m => m.RuleFor(x => x.UsuarioId).NotEmpty());
    }
}

public sealed class SalvarAssuntoValidator : AbstractValidator<SalvarAssuntoRequest>
{
    public SalvarAssuntoValidator()
    {
        RuleFor(r => r.Nome).NotEmpty().MaximumLength(200);
        RuleFor(r => r.CodigoOuvidorSus).MaximumLength(20);
        RuleFor(r => r.Ordem).GreaterThanOrEqualTo(0);
    }
}

public sealed class SalvarMarcadorValidator : AbstractValidator<SalvarMarcadorRequest>
{
    public SalvarMarcadorValidator()
    {
        RuleFor(r => r.Nome).NotEmpty().MaximumLength(80);
    }
}

public sealed class OuvidoriaConfiguracaoValidator : AbstractValidator<OuvidoriaConfiguracaoDto>
{
    public OuvidoriaConfiguracaoValidator()
    {
        RuleFor(r => r.PrazoCidadaoDias).InclusiveBetween(1, 365);
        RuleFor(r => r.ProrrogacaoDias).InclusiveBetween(1, 365);
        RuleFor(r => r.PrazoAreaDias).InclusiveBetween(1, 365);
        RuleFor(r => r.PrazoAreaAltaDias).InclusiveBetween(1, 365);
        RuleFor(r => r.PrazoAreaUrgenteDiasUteis).InclusiveBetween(1, 60);
        RuleFor(r => r.ComplementacaoDias).InclusiveBetween(1, 365);
        RuleFor(r => r.ArquivamentoAutomaticoDias).InclusiveBetween(1, 365);
        RuleFor(r => r.TextoRecibo).MaximumLength(2000);
    }
}
