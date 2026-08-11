namespace SMSMarica.Core.Cidadao.Dtos;

/// <summary>Link de acesso (magic-link) gerado para enviar ao paciente.</summary>
public sealed record MagicLinkDto(Guid Token, string Url, DateTime ExpiraEm);

/// <summary>Corpo da troca do magic-link por sessão.</summary>
public sealed record MagicLinkTrocaRequest(Guid Token);

/// <summary>Corpo da confirmação de CPF do magic-link (2º passo dos links clínicos).</summary>
public sealed record MagicLinkConfirmarRequest(Guid Token, string Cpf);

/// <summary>Agendamento confirmado pelo USO do magic link — dados para o modal "Agenda confirmada"
/// do PWA. <c>ConfirmadaAgora</c> false quando a confirmação já existia (link reaproveitado).</summary>
public sealed record ConfirmacaoAgendamentoDto(
    Guid SolicitacaoExameId, string Titulo, DateTime? InicioEm, string? Unidade, bool ConfirmadaAgora);

/// <summary>
/// Resposta da troca do magic-link. <c>Token</c> (JWT) e <c>Paciente</c> só vêm preenchidos na
/// PRIMEIRA troca (token válido e não usado) — aí o app autentica. Quando o token já foi
/// usado/expirado, ambos vêm <c>null</c> e só há <c>Destino</c>: o app abre a rota direto se JÁ
/// estiver autenticado (facilitador), e manda pro login se não estiver — nunca re-autentica.
///
/// <para><c>RequerConfirmacaoCpf</c> = link clínico ainda no DESAFIO: nada foi consumido e nada é
/// revelado (nem nome, nem CPF mascarado, nem destino). O app pede o CPF e chama
/// <c>/magic/confirmar</c>. <c>TentativasRestantes</c> só acompanha o desafio.</para>
/// </summary>
public sealed record RespostaMagicLinkDto(
    string? Token, PacienteSessaoDto? Paciente, string Destino,
    ConfirmacaoAgendamentoDto? ConfirmacaoAgendamento = null,
    bool RequerConfirmacaoCpf = false, int? TentativasRestantes = null);
