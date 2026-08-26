using SMSMais.Core.Conversas.Dtos;
using SMSMais.Core.Notificacoes.WhatsApp;

namespace SMSMais.Core.Conversas;

/// <summary>
/// Orquestra o chat multi-operador: inicia conversa por template, envia texto livre (com gate de
/// 24h e prefixo do operador), lista/lê as filas conforme a visibilidade por unidade e marca lida.
/// </summary>
public interface IConversaService
{
    /// <summary>Inicia (ou reusa) uma conversa disparando um template aprovado. Retorna o id da conversa.</summary>
    Task<Guid> IniciarComTemplateAsync(IniciarConversaRequest request, CancellationToken ct = default);

    /// <summary>Envia texto livre. Falha com <c>janela.expirada</c> fora da janela de 24h.</summary>
    Task EnviarTextoAsync(Guid conversaId, EnviarMensagemRequest request, CancellationToken ct = default);

    /// <summary>Lista as conversas visíveis ao operador atual conforme a aba.</summary>
    Task<IReadOnlyList<ConversaListItemDto>> ListarAsync(AbaConversas aba, string? busca, CancellationToken ct = default);

    /// <summary>Cabeçalho/estado de uma conversa.</summary>
    Task<ConversaListItemDto> ObterAsync(Guid conversaId, CancellationToken ct = default);

    /// <summary>Mensagens da thread (ordem cronológica).</summary>
    Task<IReadOnlyList<MensagemDto>> ObterMensagensAsync(Guid conversaId, CancellationToken ct = default);

    /// <summary>
    /// Zera o contador de não-lidas. NÃO muda a posse — o claim explícito é
    /// <see cref="AssumirAsync"/> (o front antigo chama este endpoint ao abrir a thread; se ele
    /// desse claim, abrir a conversa roubaria a posse em silêncio).
    /// </summary>
    Task MarcarLidaAsync(Guid conversaId, CancellationToken ct = default);

    /// <summary>
    /// Assume a conversa (claim): o operador vira o responsável, o contador zera e ela sai da
    /// fila para a lista pessoal dele. Conversa de outro responsável falha com
    /// <c>conversa.ja_assumida</c> (posse só muda por encaminhar/devolver/transferir).
    /// </summary>
    Task AssumirAsync(Guid conversaId, CancellationToken ct = default);

    /// <summary>
    /// Devolve a conversa à fila: limpa o responsável, mantendo a unidade (sem unidade, volta à
    /// triagem geral). Só o próprio responsável ou a supervisão.
    /// </summary>
    Task DevolverAsync(Guid conversaId, CancellationToken ct = default);

    /// <summary>
    /// Devolve a conversa ao robô ("Atendente Virtual"): volta à fila, re-arma a trava
    /// humano-por-janela e, se houver mensagem do cidadão sem resposta, o robô retoma. Só com o
    /// robô ligado. Conversa de terceiro exige supervisão.
    /// </summary>
    Task EncaminharParaRoboAsync(Guid conversaId, CancellationToken ct = default);

    /// <summary>
    /// Encaminha a conversa para outro atendente (ele vira o responsável). O alvo precisa estar
    /// ativo, ter o módulo Conversas e vínculo com a unidade da conversa. Conversa de terceiro
    /// exige supervisão.
    /// </summary>
    Task EncaminharAsync(Guid conversaId, EncaminharConversaRequest request, CancellationToken ct = default);

    /// <summary>
    /// Transfere a conversa para outra unidade: entra na fila de lá SEM responsável. Conversa de
    /// terceiro exige supervisão.
    /// </summary>
    Task TransferirUnidadeAsync(Guid conversaId, TransferirConversaRequest request, CancellationToken ct = default);

    /// <summary>Atendentes que podem receber a conversa por encaminhamento.</summary>
    Task<IReadOnlyList<AtendenteElegivelDto>> ListarAtendentesElegiveisAsync(
        Guid conversaId, CancellationToken ct = default);

    /// <summary>Unidades ativas que podem receber a conversa por transferência.</summary>
    Task<IReadOnlyList<UnidadeDestinoDto>> ListarUnidadesDestinoAsync(CancellationToken ct = default);

    /// <summary>Contadores de não-lidas (minhas × fila) para sino/badge sem carregar a lista.</summary>
    Task<ResumoConversasDto> ObterResumoAsync(CancellationToken ct = default);

    /// <summary>Templates aprovados para iniciar conversa.</summary>
    Task<IReadOnlyList<TemplateWhatsApp>> ListarTemplatesAsync(CancellationToken ct = default);

    /// <summary>
    /// Acha o contato para abrir a conversa: nº da solicitação (SISREG), CPF, CNS ou qualquer
    /// parte do nome. Termo com menos de 3 caracteres devolve vazio.
    /// </summary>
    Task<IReadOnlyList<ContatoConversaDto>> BuscarContatosAsync(string? termo, CancellationToken ct = default);

    /// <summary>
    /// TODOS os cadastros que têm o telefone desta conversa (telefone de família). O titular
    /// vem marcado. Lista vazia = ninguém no hub tem esse número.
    /// </summary>
    Task<IReadOnlyList<PacienteDoTelefoneDto>> ListarPacientesDoTelefoneAsync(
        Guid conversaId, CancellationToken ct = default);
}
