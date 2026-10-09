namespace SMSMais.Core.Cidadao.Dtos;

/// <summary>Um acesso (sessão) do cidadão — para a aba "Histórico de Acesso" no painel.</summary>
/// <param name="PersonificadoPor">Nome de quem da equipe entrou no lugar do paciente pelo
/// "Entrar como paciente" do Sandbox; null = foi o próprio paciente.</param>
public sealed record AcessoCidadaoDto(
    Guid Id,
    string Canal,
    string? Dispositivo,
    string? Ip,
    DateTime CriadaEm,
    DateTime ExpiraEm,
    DateTime? RevogadaEm,
    bool Ativa,
    string? PersonificadoPor = null);

/// <summary>Pedido de revogação global dos acessos do cidadão (botão de pânico).</summary>
/// <param name="Motivo">Fica no log — por que os acessos foram derrubados.</param>
public sealed record RevogarAcessosRequest(string Motivo);

/// <summary>O que a revogação global derrubou.</summary>
public sealed record RevogacaoGlobalDto(int LinksExpirados, int SessoesRevogadas);
