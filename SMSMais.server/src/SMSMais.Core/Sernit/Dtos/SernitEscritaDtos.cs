namespace SMSMais.Core.Sernit.Dtos;

/// <summary>Pedido de FollowUP vindo da tela.</summary>
public sealed record SernitFollowUpRequest(string Texto);

/// <summary>Um evento lido AO VIVO da trilha do SERNIT (para conferência).</summary>
public sealed record SernitEventoDiretoDto(
    string? Data,
    string? Evento,
    string? EstadoAnterior,
    string? EstadoAtual,
    string? CentralRegulacao,
    string? UnidadeExecutora,
    string? Usuario,
    string? LotacaoEvento,
    string? Ip,
    string? Observacao);

/// <summary>
/// Resultado de um FollowUP registrado no SERNIT. <see cref="MensagemDoSer"/> não é prova — quem
/// prova é <see cref="Evento"/>, achado na RELEITURA do histórico.
/// </summary>
public sealed record SernitFollowUpResultadoDto(
    string IdSernit,
    string MensagemDoSer,
    SernitEventoDiretoDto Evento,
    int EventosNovos);

/// <summary>
/// Os telefones da solicitação como o SERNIT os tem AGORA. <see cref="Editavel"/> é falso quando a
/// situação não oferece "Editar" (Cancelada, Alta) — a tela mostra os números sem o botão.
/// </summary>
public sealed record SernitContatosDto(
    string? Residencial,
    string? WhatsApp,
    string? Contato,
    bool Editavel,
    string? MotivoNaoEditavel);

/// <summary>Alteração dos telefones no SERNIT. Campo <c>null</c> = não mexer; string vazia = limpar.</summary>
public sealed record SernitAlterarContatosRequest(
    string? Residencial,
    string? WhatsApp,
    string? Contato);

/// <summary>Credencial do operador no SERNIT — trafega, valida e some. Nunca é persistida.</summary>
public sealed record SernitLoginOperadorRequest(string Usuario, string Senha);
