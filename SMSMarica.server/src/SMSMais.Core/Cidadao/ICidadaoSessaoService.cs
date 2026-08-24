namespace SMSMais.Core.Cidadao;

/// <summary>
/// Gerencia a identidade de acesso do cidadão (<c>cidadao_acesso</c>) e a
/// <b>sessão única por dispositivo</b> (<c>cidadao_sessao</c>). Toda autenticação
/// (OTP/senha/social) passa por <see cref="AbrirSessaoAsync"/>, que revoga a sessão
/// ativa anterior e emite um token novo — o aparelho antigo perde o acesso.
/// </summary>
public interface ICidadaoSessaoService
{
    /// <summary>
    /// Abre uma sessão para o cidadão (criando o <c>cidadao_acesso</c> se ainda não existir),
    /// revogando qualquer sessão ativa anterior. Retorna o JWT e a expiração.
    /// </summary>
    Task<(string Token, DateTime ExpiraEm)> AbrirSessaoAsync(
        Guid patientId, string nome, string cpf, string canal,
        string? dispositivo, string? ip, CancellationToken ct = default);

    /// <summary>
    /// Valida o jti (id da sessão) contra a sessão ativa do paciente e, de quebra,
    /// informa se o cidadão tem consentimento vigente. Usado a cada request (gate).
    /// </summary>
    Task<AcessoCidadaoValidacao> ValidarAcessoAsync(Guid sessaoJti, Guid patientId, CancellationToken ct = default);

    /// <summary>Revoga a sessão atual (logout).</summary>
    Task RevogarAsync(Guid sessaoJti, CancellationToken ct = default);

    /// <summary>
    /// BOTÃO DE PÂNICO: expira TODOS os magic links ainda válidos e revoga TODAS as sessões
    /// ativas de cidadão. Para quando um lote de mensagens pode ter ido para números errados —
    /// nenhum link antigo autentica mais e quem estiver logado cai. O paciente certo reentra
    /// pelo link novo (ou pelo OTP). Devolve quantos links e sessões foram derrubados.
    /// </summary>
    Task<(int Links, int Sessoes)> RevogarTodosAcessosAsync(string motivo, CancellationToken ct = default);

    /// <summary>
    /// Histórico de acessos (sessões) do paciente, mais recentes primeiro. Usado pelo
    /// painel (staff) na aba "Histórico de Acesso" do cadastro do paciente.
    /// </summary>
    Task<IReadOnlyList<Dtos.AcessoCidadaoDto>> ListarAcessosAsync(
        Guid patientId, CancellationToken ct = default);
}

/// <summary>Resultado do gate por requisição: sessão ativa? consentimento vigente?</summary>
public readonly record struct AcessoCidadaoValidacao(bool SessaoValida, bool Consentido);
