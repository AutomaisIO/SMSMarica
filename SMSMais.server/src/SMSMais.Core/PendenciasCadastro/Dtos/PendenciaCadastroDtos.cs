using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.PendenciasCadastro.Dtos;

public sealed record PendenciaCadastroListItemDto(
    Guid Id,
    string TelefoneCanonical,
    Guid? PacienteId,
    string? PacienteNome,
    string? PacienteCpf,
    TipoPendenciaCadastro Tipo,
    VinculoContato Vinculo,
    string? Observacao,
    StatusPendenciaCadastro Status,
    bool CriadaPeloRobo,
    DateTime CriadoEm,
    DateTime? ResolvidoEm,
    string? ResolucaoNota);

public sealed record ResolverPendenciaRequest(string? Nota);

/// <summary>Pedido da varredura de contato negado (janela em horas; 48 por padrão).</summary>
public sealed record VarreduraContatoNegadoRequest(int Horas = 48);

/// <summary>Um caso avaliado pela varredura. <paramref name="Acao"/>: "pendencia-registrada",
/// "ja-existia" ou "descartada-pelo-modelo".</summary>
public sealed record VarreduraContatoNegadoItemDto(
    Guid ConversaId,
    string TelefoneCanonical,
    Guid? PacienteId,
    string Acao,
    string? Resumo);

/// <summary>Relatório da varredura (etapa 1 = padrões amplos; etapa 2 = Haiku).</summary>
public sealed class VarreduraContatoNegadoResultadoDto
{
    public int Horas { get; init; }
    public int MensagensLidas { get; init; }
    public int ConversasCandidatas { get; init; }
    public int Erros { get; set; }
    public List<VarreduraContatoNegadoItemDto> Itens { get; } = [];
    public int PendenciasRegistradas => Itens.Count(i => i.Acao == "pendencia-registrada");
    public int JaExistiam => Itens.Count(i => i.Acao == "ja-existia");
    public int DescartadasPeloModelo => Itens.Count(i => i.Acao == "descartada-pelo-modelo");
}
