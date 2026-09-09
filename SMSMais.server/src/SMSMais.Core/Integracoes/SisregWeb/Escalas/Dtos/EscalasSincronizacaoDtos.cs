using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.Escalas.Dtos;

/// <summary>Resposta ao disparo manual (202 — roda no servidor, fechar a aba não interrompe).</summary>
public sealed record EscalasSincronizacaoAceitaDto(Guid ExecucaoId, string Mensagem);

/// <summary>
/// Progresso da sincronização em curso. <c>null</c> no endpoint = nada rodando.
///
/// <para><see cref="Fase"/> é texto humano de propósito: a maior parte do tempo é o download do
/// arquivo (5,9 MB), quando ainda não existe denominador nenhum. Sem a frase, a tela ficaria em
/// "0 de 0" parecendo travada.</para>
/// </summary>
public sealed record EscalasSincronizacaoStatusDto(
    bool EmExecucao,
    DisparoSincronizacao Disparo,
    string Fase,
    int EscalasLidas,
    int EscalasGravadas,
    int EscalasNovas,
    int EscalasAtualizadas,
    int LinhasRejeitadas,
    int UnidadesNaoEncontradas,
    DateTime IniciadoEm,
    string? UltimoErro);

/// <summary>Uma sincronização encerrada — o que sobra depois que o progresso vivo some.</summary>
public sealed record EscalasSincronizacaoExecucaoDto(
    Guid Id,
    DisparoSincronizacao Disparo,
    StatusVarredura Status,
    int EscalasLidas,
    int EscalasNovas,
    int EscalasAtualizadas,
    int EscalasAusentes,
    int LinhasRejeitadas,
    int UnidadesNaoEncontradas,
    int Requisicoes,
    string? MensagemErro,
    DateTime IniciadoEm,
    DateTime? FinalizadoEm,
    int? DuracaoSegundos,
    string? CriadoPorNome);

/// <summary>Configuração do disparo diário. Guardada no <c>ParametrosJson</c> da credencial
/// <c>sisreg</c>, junto com o agendamento do lote de mapeamento — não em tabela nova.</summary>
/// <param name="HoraLocal">
/// Primeiro horário da lista. Mantido para não quebrar quem lê o campo único; a verdade é
/// <paramref name="HorariosLocais"/>.
/// </param>
/// <param name="HorariosLocais">Todos os horários do dia, normalizados (HH:mm) e ordenados.</param>
public sealed record EscalasAgendamentoDto(
    bool Ativo,
    string HoraLocal,
    IReadOnlyList<string> HorariosLocais,
    int OrcamentoRestante);

/// <param name="HoraLocal">Caminho antigo, de um horário só. Ignorado quando
/// <paramref name="HorariosLocais"/> vem preenchido.</param>
/// <param name="HorariosLocais">Lista de horários; tem precedência.</param>
public sealed record SalvarEscalasAgendamentoRequest(
    bool Ativo,
    string? HoraLocal = null,
    IReadOnlyList<string>? HorariosLocais = null);
