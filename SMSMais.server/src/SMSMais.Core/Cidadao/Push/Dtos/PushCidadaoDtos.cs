namespace SMSMais.Core.Cidadao.Push.Dtos;

/// <summary>App → servidor: o token do Firebase deste aparelho, preso à sessão do <c>jti</c>.</summary>
public sealed record RegistrarDispositivoRequest(string? Token, string? Plataforma);

/// <summary>Painel: o paciente tem o app com notificações? E o que já foi mandado a ele.</summary>
public sealed record AppCidadaoStatusDto(
    bool Configurado,
    IReadOnlyList<AparelhoAppCidadaoDto> Aparelhos,
    IReadOnlyList<NotificacaoAppCidadaoDto> Notificacoes);

/// <summary>Sessão ativa com token. <see cref="EntrouEm"/> = quando a sessão foi aberta (login).</summary>
public sealed record AparelhoAppCidadaoDto(
    Guid SessaoId,
    string Plataforma,
    DateTime RegistradoEm,
    DateTime EntrouEm,
    string? Dispositivo);

public sealed record NotificacaoAppCidadaoDto(
    Guid Id,
    string Titulo,
    string Mensagem,
    string? Rota,
    DateTime CriadaEm,
    string? EnviadaPor,
    int Aparelhos,
    int Entregues,
    string? Falha);

/// <summary>Painel → servidor. <see cref="Rota"/> null = o toque abre o app no Início.</summary>
public sealed record EnviarNotificacaoAppRequest(string? Titulo, string? Mensagem, string? Rota);

public sealed record EnvioNotificacaoAppDto(
    Guid NotificacaoId,
    int Aparelhos,
    int Entregues,
    IReadOnlyList<ResultadoEnvioAparelhoDto> Resultados);

/// <summary><see cref="AparelhoRemovido"/> = o Firebase disse que o token morreu e ele saiu da sessão.</summary>
public sealed record ResultadoEnvioAparelhoDto(
    string Plataforma,
    bool Entregue,
    string? Detalhe,
    bool AparelhoRemovido);

/// <summary>"Testar" da credencial do Firebase em Integrações. Nunca lança: o desfecho vem aqui.</summary>
public sealed record TesteCredencialFcmDto(bool Ok, string? ProjectId, string Mensagem);
