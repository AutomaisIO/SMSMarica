using SMSMarica.Core.Conversas.RespostasRapidas.Dtos;

namespace SMSMarica.Core.Conversas.RespostasRapidas;

/// <summary>
/// Mensagens prontas do chat: cadastro (tela de gestão) e uso (lista lateral da conversa).
/// Cadastrar exige o módulo RespostasRapidas; usar, só Conversas.
/// </summary>
public interface IRespostaRapidaService
{
    /// <summary>Tudo o que o operador atual enxerga: as globais + as das unidades dele.</summary>
    Task<IReadOnlyList<RespostaRapidaDto>> ListarAsync(bool incluirInativas, CancellationToken ct = default);

    Task<RespostaRapidaDto> ObterAsync(Guid id, CancellationToken ct = default);

    Task<Guid> CriarAsync(SalvarRespostaRapidaRequest request, CancellationToken ct = default);

    Task AtualizarAsync(Guid id, SalvarRespostaRapidaRequest request, CancellationToken ct = default);

    /// <summary>Exclusão lógica — o histórico das mensagens já enviadas não muda.</summary>
    Task ExcluirAsync(Guid id, CancellationToken ct = default);

    /// <summary>Catálogo das tags automáticas (o que o cadastro pode usar sem declarar campo).</summary>
    IReadOnlyList<TagAutomaticaDto> ListarTagsAutomaticas();

    /// <summary>
    /// Resolve o corpo no contexto de uma conversa: as tags automáticas saem do paciente/
    /// operador/unidade e as manuais dos valores digitados. O texto volta para o campo de
    /// digitação — nada é enviado aqui.
    /// </summary>
    Task<TextoResolvidoDto> ResolverAsync(
        Guid conversaId,
        Guid respostaRapidaId,
        ResolverRespostaRapidaRequest request,
        CancellationToken ct = default);
}
