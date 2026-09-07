using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Regulacao.Configuracao;

namespace SMSMais.Core.Regulacao.Legado;

/// <summary>Como a tela antiga deve se comportar agora.</summary>
/// <param name="SomenteLeitura">
/// Verdadeiro depois da migração: a página continua abrindo e mostrando o que existe, mas não
/// aceita mais criar, editar, anexar nem excluir.
/// </param>
/// <param name="MigradoEm">Quando o corte foi feito. <c>null</c> = tela ainda em uso normal.</param>
/// <param name="Substituto">Rota do wizard que substitui esta tela.</param>
public sealed record EstadoRascunhoLegadoDto(bool SomenteLeitura, DateTime? MigradoEm, string Substituto);

/// <summary>
/// Ponto único que decide se os rascunhos por sistema (<c>ser_*</c>, <c>sernit_*</c>) ainda
/// aceitam escrita.
///
/// <para><b>Por que um gate e não um <c>if</c> em cada controller:</b> são dez ações de escrita
/// entre SER e SERNIT, e uma esquecida é justamente a que deixa alguém editar um rascunho que já
/// virou solicitação da regulação — a edição some sem ninguém notar. O guard fica no serviço,
/// e não no controller, para valer também para qualquer chamador interno.</para>
/// </summary>
public interface IRascunhoLegadoGate
{
    Task<EstadoRascunhoLegadoDto> EstadoAsync(CancellationToken ct);

    /// <summary>
    /// Lança <see cref="RecursoDescontinuadoException"/> (410) se os rascunhos já foram migrados.
    /// </summary>
    Task GarantirEscritaPermitidaAsync(string sistema, CancellationToken ct);
}

/// <inheritdoc cref="IRascunhoLegadoGate"/>
public sealed class RascunhoLegadoGate(IRegulacaoConfiguracaoService configuracao) : IRascunhoLegadoGate
{
    /// <summary>Rota do wizard novo. Uma constante só, para as duas telas e o 410 dizerem o mesmo.</summary>
    public const string RotaWizard = "/app/regulacao/solicitacoes/nova";

    public async Task<EstadoRascunhoLegadoDto> EstadoAsync(CancellationToken ct)
    {
        var config = await configuracao.ObterEntidadeAsync(ct);
        return new EstadoRascunhoLegadoDto(
            SomenteLeitura: config.RascunhosLegadosMigradosEm is not null,
            MigradoEm: config.RascunhosLegadosMigradosEm,
            Substituto: RotaWizard);
    }

    public async Task GarantirEscritaPermitidaAsync(string sistema, CancellationToken ct)
    {
        var estado = await EstadoAsync(ct);
        if (!estado.SomenteLeitura) return;

        throw new RecursoDescontinuadoException(
            "regulacao.rascunho_legado.migrado",
            $"Os rascunhos do {sistema} foram migrados para Regulação → Solicitações e esta tela "
            + "ficou somente-leitura. Abra a solicitação pelo caminho novo.",
            RotaWizard);
    }
}
