using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote.Dtos;

/// <summary>Resposta ao disparo do lote: aceito (202) e enfileirado.</summary>
public sealed record MapeamentoLoteAceitoDto(int UnidadesTotal, string Mensagem);

/// <summary>
/// Snapshot do lote em curso (ou <c>null</c> quando não há nenhum). É memória, não banco: só o
/// processo sabe se ainda está vivo. O que sobra depois da execução está em
/// <see cref="MapeamentoLoteExecucaoDto"/>.
/// </summary>
public sealed record MapeamentoLoteStatusDto(
    bool EmExecucao,
    DisparoSincronizacao Disparo,
    /// <summary>Fase atual, para a tela não mostrar "0/43" enquanto ainda está descobrindo.</summary>
    string Fase,
    int UnidadesTotal,
    int UnidadesFeitas,
    string? UnidadeAtual,
    int UnidadesNoSisreg,
    int UnidadesCriadas,
    int UnidadesMapeadas,
    int UnidadesPuladas,
    int RequisicoesFeitas,
    int ProfissionaisEncontrados,
    int ProfissionaisNovos,
    int ProcedimentosEncontrados,
    int ProcedimentosNovos,
    int PractitionersCriados,
    int PractitionersVinculados,
    int UnidadesComErro,
    /// <summary>Requisições ainda disponíveis na janela de 60 min antes do teto anti-robô.</summary>
    int OrcamentoRestante,
    DateTime IniciadoEm,
    string? UltimoErro);

/// <summary>Configuração do disparo diário automático do lote de mapeamento.</summary>
public sealed record MapeamentoLoteAgendamentoDto(bool Ativo, string HoraLocal);

/// <summary>Ligar/desligar o disparo diário e a que horas (Brasília, HH:mm).</summary>
public sealed record SalvarMapeamentoLoteAgendamentoRequest(bool Ativo, string HoraLocal);

/// <summary>Uma execução do "sincroniza tudo", para a lista de sincronizações recentes.</summary>
public sealed record MapeamentoLoteExecucaoDto(
    Guid Id,
    DisparoSincronizacao Disparo,
    StatusVarredura Status,
    int UnidadesNoSisreg,
    int UnidadesCriadas,
    int UnidadesComCnesPreenchido,
    int UnidadesTotal,
    int UnidadesMapeadas,
    int UnidadesPuladas,
    int UnidadesComErro,
    int ProfissionaisEncontrados,
    int ProfissionaisNovos,
    int ProcedimentosEncontrados,
    int ProcedimentosNovos,
    int PractitionersCriados,
    int PractitionersVinculados,
    int Requisicoes,
    string? MensagemErro,
    DateTime IniciadoEm,
    DateTime? FinalizadoEm,
    int? DuracaoSegundos);

/// <summary>Detalhe por unidade de uma execução — o "quantos médicos por unidade" da tela.</summary>
public sealed record MapeamentoLoteExecucaoItemDto(
    Guid Id,
    Guid UnidadeId,
    string UnidadeNome,
    string? Cnes,
    bool UnidadeCriada,
    ResultadoUnidadeLote Resultado,
    int ProfissionaisEncontrados,
    int ProfissionaisNovos,
    int ProfissionaisAusentes,
    int ProcedimentosEncontrados,
    int ProcedimentosNovos,
    int PractitionersCriados,
    int PractitionersVinculados,
    int Requisicoes,
    string? Observacao);
