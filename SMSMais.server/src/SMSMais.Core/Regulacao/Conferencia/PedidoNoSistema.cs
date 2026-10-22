using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Regulacao.Conferencia;

/// <summary>
/// O pedido como o sistema de regulação o mostra, lido pelo número — no vocabulário comum aos
/// quatro sistemas.
///
/// <para><b>Nulo é "esse sistema não mostra"</b>, não "está vazio lá": o SER não mostra risco nem
/// médico na grade, o ESUS não mostra CID. A conferência não acusa diferença onde não há o que
/// comparar.</para>
/// </summary>
public sealed record PedidoNoSistema(
    string? Situacao,
    string? PacienteNome,
    string? PacienteCpf,
    string? PacienteCns,
    IReadOnlyList<ProcedimentoNoSistema> Procedimentos,
    string? Cid,
    string? UnidadeSolicitante,
    string? UnidadeSolicitanteCnes,
    string? MedicoNome,
    string? MedicoCpf,
    string? Risco,
    string? DataSolicitacao);

/// <param name="Codigo">O código que o sistema mostra junto (SISREG: código interno), quando mostra.</param>
public sealed record ProcedimentoNoSistema(string Nome, string? Codigo);

/// <summary>
/// Lê UM pedido pelo número no sistema de regulação, com a credencial institucional (a do
/// sincronismo) — nunca com a senha pessoal de quem clicou. <b>Só leitura.</b>
/// </summary>
public interface ILeitorPedidoExterno
{
    SistemaRegulacao Sistema { get; }

    /// <returns><c>null</c> quando o sistema não achou o número.</returns>
    Task<PedidoNoSistema?> LerAsync(string numero, CancellationToken ct);
}
