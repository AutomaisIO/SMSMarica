using SMSMarica.Core.Institucional.Dtos;

namespace SMSMarica.Core.Institucional;

/// <summary>
/// Acesso à identidade da instituição desta instância (ADR-0043).
///
/// <para>
/// É lido em caminho quente — toda página pública, todo PDF, toda normalização de telefone —
/// por isso a implementação mantém cache em memória, invalidado no <see cref="SalvarAsync"/>.
/// </para>
/// </summary>
public interface IInstituicaoService
{
    /// <summary>
    /// Identidade configurada. Nunca lança: numa instância recém-provisionada, antes de
    /// alguém preencher a tela, devolve o padrão neutro de <see cref="ObterPadrao"/> —
    /// o sistema tem que subir e permitir o login que vai configurá-lo.
    /// </summary>
    Task<InstituicaoDto> ObterAsync(CancellationToken cancellationToken = default);

    /// <summary>Salva (upsert) a identidade e derruba o cache.</summary>
    Task<InstituicaoDto> SalvarAsync(
        Guid usuarioId,
        SalvarInstituicaoRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Padrão neutro usado enquanto a tabela está vazia. Deliberadamente <b>sem</b> nome de
    /// município: uma instância nova que ainda não foi configurada não pode se apresentar
    /// como outra prefeitura.
    /// </summary>
    static InstituicaoDto ObterPadrao() => new(
        Nome: "Prefeitura Municipal",
        NomeSecretaria: "Secretaria Municipal de Saúde",
        NomeCurto: "Saúde",
        Sigla: null,
        Cnpj: null,
        CodigoIbge: null,
        Uf: string.Empty,
        DddPadrao: null,
        Endereco: null,
        Telefone: null,
        EmailContato: null,
        EmailDpo: null,
        WhatsAppNumeroPublico: null,
        LogoMidiaId: null,
        FaviconMidiaId: null,
        CorPrimaria: null,
        CorSecundaria: null,
        CorGradienteInicio: null,
        CorGradienteFim: null,
        UrlPainel: null,
        UrlApp: null,
        UrlArquivos: null,
        AssinaturaProdutoHtml: null,
        AtualizadoEm: null);
}
