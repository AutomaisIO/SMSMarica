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

    /// <summary>Zera o contador de não-lidas ao abrir a conversa.</summary>
    Task MarcarLidaAsync(Guid conversaId, CancellationToken ct = default);

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
