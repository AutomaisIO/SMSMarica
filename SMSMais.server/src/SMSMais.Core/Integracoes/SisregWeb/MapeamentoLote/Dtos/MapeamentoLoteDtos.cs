using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote.Dtos;

/// <summary>Resposta ao disparo do lote: aceito (202) e enfileirado.</summary>
public sealed record MapeamentoLoteAceitoDto(int UnidadesTotal, string Mensagem);

/// <summary>
/// Snapshot do lote em curso (ou <c>null</c> quando não há nenhum). É memória, não banco: só o
/// processo sabe se ainda está vivo.
/// </summary>
public sealed record MapeamentoLoteStatusDto(
    bool EmExecucao,
    DisparoSincronizacao Disparo,
    int UnidadesTotal,
    int UnidadesFeitas,
    string? UnidadeAtual,
    int RequisicoesFeitas,
    int ProfissionaisEncontrados,
    int ProfissionaisNovos,
    int PractitionersCriados,
    int PractitionersVinculados,
    int UnidadesComErro,
    DateTime IniciadoEm,
    string? UltimoErro);

/// <summary>Configuração do disparo diário automático do lote de mapeamento.</summary>
public sealed record MapeamentoLoteAgendamentoDto(bool Ativo, string HoraLocal);

/// <summary>Ligar/desligar o disparo diário e a que horas (Brasília, HH:mm).</summary>
public sealed record SalvarMapeamentoLoteAgendamentoRequest(bool Ativo, string HoraLocal);
